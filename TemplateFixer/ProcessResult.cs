namespace TemplateFixer
{
    internal class ProcessResult
    {
        public ProcessResult(string output, int exitCode)
        {
            Output = output;
            ExitCode = exitCode;
        }

        public string Output { get; }

        public int ExitCode { get; }
    }
}