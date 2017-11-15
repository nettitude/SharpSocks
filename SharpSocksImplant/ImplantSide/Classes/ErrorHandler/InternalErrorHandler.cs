using ImplantSide.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImplantSide.Classes.ErrorHandler
{
    public class InternalErrorHandler : IInternalErrorHandler
    {
        IImplantLog _implantComms;
        public bool VerboseErrors { get; set; }

        public InternalErrorHandler(IImplantLog implantComms)
        {
            _implantComms = implantComms;
        }
        public void FailError(List<String> errors)
        {
            //Basically its unrecoverable
            var sb = new StringBuilder();
            errors.ForEach(x =>
            {
                sb.Append(x);
                if (!(x.EndsWith(".")))
                    sb.AppendLine(". ");
            });
            throw new Exception(sb.ToString());
        }

        public void FailError(String error)
        {
            _implantComms.FailError(error, Guid.Empty);
            throw new Exception(error);
        }

        public void LogError(List<String> errors)
        {
            var sb = new StringBuilder();
            errors.ForEach(x =>
            {
                sb.AppendLine(x);
            });
            _implantComms.LogError(sb.ToString());
        }

        public void LogError(String error)
        {
            _implantComms.LogError(error);
        }

        public void LogVerboseError(String error)
        {
            if (VerboseErrors)
                _implantComms.LogError(error);
        }
    }
}
