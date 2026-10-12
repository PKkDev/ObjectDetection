using FrameAnalysis.Filtering;
using YOLO26.Shared.YOLOWorker.DataStructures;

namespace ObjectDetection.Tests
{
    public class DetectionChangeFilterTests
    {
        private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        private static YOLO26Result Det(string label, float score = 0.9f)
            => new() { Label = label, Score = score, X1 = 0, Y1 = 0, X2 = 10, Y2 = 10 };

        private static DateTimeOffset At(double seconds) => Start.AddSeconds(seconds);

        /// <summary>
        /// Фильтр без ожидания устойчивости и без паузы - чтобы проверять по одному правилу за раз
        /// </summary>
        private static DetectionChangeFilter ImmediateFilter(Action<FrameFilterOptions>? configure = null)
        {
            var options = new FrameFilterOptions { StableFrames = 1, Cooldown = TimeSpan.Zero };
            configure?.Invoke(options);
            return new DetectionChangeFilter(options);
        }

        [Fact]
        public void EmptyScene_NeverTriggers()
        {
            var filter = ImmediateFilter();

            Assert.False(filter.ShouldAnalyze([], At(0)));
            Assert.False(filter.ShouldAnalyze([], At(1)));
        }

        [Fact]
        public void ObjectAppears_TriggersOnce_ThenStaysQuietWhileSceneIsUnchanged()
        {
            var filter = ImmediateFilter();

            Assert.True(filter.ShouldAnalyze([Det("car")], At(0)));
            Assert.False(filter.ShouldAnalyze([Det("car")], At(1)));
            Assert.False(filter.ShouldAnalyze([Det("car")], At(2)));
        }

        [Fact]
        public void CountChanges_TriggersAgain()
        {
            var filter = ImmediateFilter();
            filter.ShouldAnalyze([Det("car")], At(0));

            Assert.True(filter.ShouldAnalyze([Det("car"), Det("car")], At(1)));
        }

        [Fact]
        public void ObjectDisappears_Triggers()
        {
            var filter = ImmediateFilter();
            filter.ShouldAnalyze([Det("car")], At(0));

            Assert.True(filter.ShouldAnalyze([], At(1)));
        }

        [Fact]
        public void SameCountDifferentLabel_Triggers()
        {
            var filter = ImmediateFilter();
            filter.ShouldAnalyze([Det("car")], At(0));

            Assert.True(filter.ShouldAnalyze([Det("truck")], At(1)));
        }

        [Fact]
        public void OrderOfDetections_DoesNotMatter()
        {
            var filter = ImmediateFilter();
            filter.ShouldAnalyze([Det("car"), Det("person")], At(0));

            Assert.False(filter.ShouldAnalyze([Det("person"), Det("car")], At(1)));
        }

        [Fact]
        public void DetectionsBelowMinScore_AreIgnored()
        {
            var filter = ImmediateFilter(o => o.MinScore = 0.5f);

            Assert.False(filter.ShouldAnalyze([Det("car", score: 0.3f)], At(0)));
            Assert.True(filter.ShouldAnalyze([Det("car", score: 0.6f)], At(1)));
        }

        [Fact]
        public void UnwatchedLabels_AreIgnored()
        {
            var filter = ImmediateFilter(o => o.WatchedLabels = new HashSet<string> { "car" });

            Assert.False(filter.ShouldAnalyze([Det("person")], At(0)));
            Assert.True(filter.ShouldAnalyze([Det("person"), Det("car")], At(1)));
            // человек ушел - для фильтра ничего не изменилось
            Assert.False(filter.ShouldAnalyze([Det("car")], At(2)));
        }

        [Fact]
        public void Change_MustHoldForStableFrames()
        {
            var filter = ImmediateFilter(o => o.StableFrames = 3);

            Assert.False(filter.ShouldAnalyze([Det("car")], At(0)));
            Assert.False(filter.ShouldAnalyze([Det("car")], At(1)));
            Assert.True(filter.ShouldAnalyze([Det("car")], At(2)));
        }

        [Fact]
        public void FlickeringDetection_DoesNotTrigger()
        {
            var filter = ImmediateFilter(o => o.StableFrames = 3);

            // объект то появляется, то пропадает - счетчик устойчивости каждый раз сбрасывается
            for (int i = 0; i < 10; i++)
            {
                IReadOnlyList<YOLO26Result> detections = i % 2 == 0 ? [Det("car")] : [];
                Assert.False(filter.ShouldAnalyze(detections, At(i)));
            }
        }

        [Fact]
        public void ChangeDuringCooldown_IsDelayedNotLost()
        {
            var filter = ImmediateFilter(o => o.Cooldown = TimeSpan.FromSeconds(30));

            Assert.True(filter.ShouldAnalyze([Det("car")], At(0)));
            Assert.False(filter.ShouldAnalyze([Det("car"), Det("car")], At(10)));
            Assert.False(filter.ShouldAnalyze([Det("car"), Det("car")], At(29)));
            Assert.True(filter.ShouldAnalyze([Det("car"), Det("car")], At(30)));
        }

        [Fact]
        public void ChangeRevertedDuringCooldown_DoesNotTrigger()
        {
            var filter = ImmediateFilter(o => o.Cooldown = TimeSpan.FromSeconds(30));
            filter.ShouldAnalyze([Det("car")], At(0));

            Assert.False(filter.ShouldAnalyze([Det("car"), Det("car")], At(10)));
            Assert.False(filter.ShouldAnalyze([Det("car")], At(40)));
        }

        [Fact]
        public void Condition_BlocksEventUntilItHolds()
        {
            var filter = ImmediateFilter(o => o.Condition = detections => detections.Count >= 2);

            Assert.False(filter.ShouldAnalyze([Det("car")], At(0)));
            Assert.True(filter.ShouldAnalyze([Det("car"), Det("car")], At(1)));
        }

        [Fact]
        public void Condition_ReceivesOnlyRelevantDetections()
        {
            IReadOnlyList<YOLO26Result>? seen = null;
            var filter = ImmediateFilter(o =>
            {
                o.WatchedLabels = new HashSet<string> { "car" };
                o.MinScore = 0.5f;
                o.Condition = detections => { seen = detections; return true; };
            });

            filter.ShouldAnalyze([Det("car"), Det("person"), Det("car", score: 0.2f)], At(0));

            Assert.NotNull(seen);
            var only = Assert.Single(seen);
            Assert.Equal("car", only.Label);
            Assert.Equal(0.9f, only.Score);
        }

        [Fact]
        public void DefaultOptions_RequireThreeStableFrames()
        {
            var filter = new DetectionChangeFilter();

            Assert.False(filter.ShouldAnalyze([Det("car")], At(0)));
            Assert.False(filter.ShouldAnalyze([Det("car")], At(1)));
            Assert.True(filter.ShouldAnalyze([Det("car")], At(2)));
        }
    }
}
