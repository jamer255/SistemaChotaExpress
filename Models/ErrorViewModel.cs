namespace SistemaChotaExpress.Models
{
    public class ErrorViewModel
    {
        public string? RequestId { get; set; }
        public string? ExceptionMessage { get; set; }
        public string? ExceptionPath { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
        public bool HasDetails => !string.IsNullOrEmpty(ExceptionMessage);
    }
}
