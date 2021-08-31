using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;

namespace SharpSocksServer
{
  [GeneratedCode("System.Resources.Tools.StronglyTypedResourceBuilder", "15.0.0.0")]
  [DebuggerNonUserCode]
  [CompilerGenerated]
  internal class SharpSocks
  {
    private static ResourceManager resourceMan;
    private static CultureInfo resourceCulture;

    internal SharpSocks()
    {
    }

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    internal static ResourceManager ResourceManager
    {
      get
      {
        if (SharpSocks.resourceMan == null)
          SharpSocks.resourceMan = new ResourceManager("SharpSocksServer.SharpSocks", typeof (SharpSocks).Assembly);
        return SharpSocks.resourceMan;
      }
    }

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    internal static CultureInfo Culture
    {
      get => SharpSocks.resourceCulture;
      set => SharpSocks.resourceCulture = value;
    }

    internal static byte[] host2cert_pfx => (byte[]) SharpSocks.ResourceManager.GetObject(nameof (host2cert_pfx), SharpSocks.resourceCulture);
  }
}
