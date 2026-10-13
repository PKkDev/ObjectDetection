namespace FrameAnalysis.Llm.Answerers.Ollama
{
    public class OllamaAnswererOptions
    {
        /// <summary>
        /// Адрес локального сервера Ollama. 127.0.0.1:11434 - адрес, на котором Ollama слушает по умолчанию;
        /// меняется на стороне Ollama переменной окружения OLLAMA_HOST
        /// </summary>
        public Uri BaseAddress { get; set; } = new("http://localhost:11434");

        /// <summary>
        /// Модель с поддержкой изображений; должна быть заранее скачана командой "ollama pull"
        /// </summary>
        public OllamaModel Model { get; set; } = OllamaModel.Gemma3_4b;

        /// <summary>
        /// Локальная модель отвечает медленно, особенно при первом обращении, когда она загружается в память
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Размер окна контекста в токенах. Если запрос с изображениями в него не помещается,
        /// Ollama возвращает ошибку 400; разным моделям на одно изображение нужно разное число токенов
        /// </summary>
        public int ContextSize { get; set; } = 8192;

        public FramePromptOptions Prompt { get; set; } = new();
    }
}
