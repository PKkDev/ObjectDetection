using System.Text.Json;
using FrameAnalysis.Llm;

namespace ObjectDetection.Tests
{
    public class FrameAnswerTests
    {
        [Fact]
        public void Parse_ValidJson_ReadsAllFields()
        {
            var answer = FrameAnswer.Parse(
                """{"answer": "Да, багажник открыт", "can_answer": true, "confidence": "high", "referenced_objects": [1, 3]}""");

            Assert.Equal("Да, багажник открыт", answer.Answer);
            Assert.True(answer.CanAnswer);
            Assert.Equal(AnswerConfidence.High, answer.Confidence);
            Assert.Equal([1, 3], answer.ReferencedObjects);
        }

        [Fact]
        public void Parse_CannotAnswer_IsNotAnError()
        {
            var answer = FrameAnswer.Parse(
                """{"answer": "Номер не читается: слишком мелко", "can_answer": false, "confidence": "low", "referenced_objects": []}""");

            Assert.False(answer.CanAnswer);
            Assert.Equal(AnswerConfidence.Low, answer.Confidence);
            Assert.Empty(answer.ReferencedObjects);
        }

        [Theory]
        [InlineData("low", AnswerConfidence.Low)]
        [InlineData("medium", AnswerConfidence.Medium)]
        [InlineData("HIGH", AnswerConfidence.High)]
        public void Parse_MapsConfidence(string value, AnswerConfidence expected)
        {
            var answer = FrameAnswer.Parse(
                $$"""{"answer": "a", "can_answer": true, "confidence": "{{value}}", "referenced_objects": []}""");

            Assert.Equal(expected, answer.Confidence);
        }

        [Fact]
        public void Parse_MissingReferencedObjects_GivesEmptyList()
        {
            var answer = FrameAnswer.Parse("""{"answer": "a", "can_answer": true, "confidence": "medium"}""");

            Assert.Empty(answer.ReferencedObjects);
        }

        [Theory]
        [InlineData("не JSON")]
        [InlineData("")]
        [InlineData("""{"answer": "a", "can_answer": true}""")]
        [InlineData("""{"can_answer": true, "confidence": "high"}""")]
        [InlineData("""{"answer": "a", "can_answer": true, "confidence": "очень"}""")]
        public void Parse_InvalidAnswer_ThrowsFormatException(string json)
        {
            Assert.Throws<FormatException>(() => FrameAnswer.Parse(json));
        }

        [Fact]
        public void JsonSchema_IsClosedObjectWithAllFieldsRequired()
        {
            var schema = FrameAnswer.JsonSchema;

            Assert.Equal("object", schema["type"].GetString());
            Assert.False(schema["additionalProperties"].GetBoolean());

            var properties = schema["properties"].EnumerateObject().Select(p => p.Name).Order().ToArray();
            var required = schema["required"].EnumerateArray().Select(e => e.GetString()!).Order().ToArray();
            Assert.Equal(["answer", "can_answer", "confidence", "referenced_objects"], properties);
            Assert.Equal(properties, required);

            var confidence = schema["properties"].GetProperty("confidence").GetProperty("enum");
            Assert.Equal(["low", "medium", "high"], confidence.EnumerateArray().Select(e => e.GetString()!));
        }
    }
}
