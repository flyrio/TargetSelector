using System.Collections;
using System.Linq;
using ECommons;
using ECommons.DalamudServices;

namespace TargetSelector;

public class Whitelist
{
    public static bool IsWhitelistedUser()
    {
        string[] whitelistedIds = new[]
        {
            "18014449513747129",
            "18014469509910844",
            "19014409517655056",
            "18014449510892528",
            "18014449513254236",
            "18014479510447116",
            "19014409509701765",
        };

        return whitelistedIds.Contains(Svc.ClientState.LocalContentId.ToString());
    }
}
