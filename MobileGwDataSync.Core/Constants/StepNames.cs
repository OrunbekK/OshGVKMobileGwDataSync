namespace MobileGwDataSync.Core.Constants
{
    public static class StepNames
    {
        public const string Initialize = "Initialize";
        public const string FetchData = "FetchData";
        public const string ValidateData = "ValidateData";
        public const string PrepareTarget = "PrepareTarget";
        public const string TransferData = "TransferData";
        public const string ProcessBatch = "ProcessBatch";
        public const string FinalizeTarget = "FinalizeTarget";
        public const string Cleanup = "Cleanup";
    }
}
