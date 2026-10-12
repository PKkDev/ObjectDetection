namespace FrameAnalysis.Llm.Answerers.Claude
{
    public class ClaudeAnswererOptions
    {
        public string Model { get; set; } = "claude-opus-5-5";

        public int MaxTokens { get; set; } = 16000;

        public FramePromptOptions Prompt { get; set; } = new();
    }
}
