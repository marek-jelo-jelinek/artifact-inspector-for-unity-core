namespace Tesearis.ArtifactInspectorForUnity.Core.Native
{
    public sealed class NativeCallException : ArtifactInspectorException
    {
        public ReturnCode ReturnCode { get; }

        private NativeCallException(string message, ReturnCode returnCode) : base(message)
        {
            ReturnCode = returnCode;
        }

        internal static NativeCallException FromReturnCode(ReturnCode code, string operationDescription)
        {
            return new NativeCallException("Native call '" + operationDescription + "' failed with " + code + ".", code);
        }
    }
}
