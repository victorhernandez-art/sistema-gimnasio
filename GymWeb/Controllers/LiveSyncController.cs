using GymWeb.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymWeb.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class LiveSyncController : ControllerBase
{
    [HttpGet("version")]
    public IActionResult GetVersion([FromQuery] string modulo = "")
    {
        modulo = (modulo ?? "").ToLowerInvariant().Trim();
        long ver = modulo switch
        {
            "socios" => LiveSyncService.VersionSocios,
            "visitas" or "registro" => LiveSyncService.VersionVisitas,
            "ventas" or "pagos" => LiveSyncService.VersionVentas,
            _ => LiveSyncService.VersionGlobal
        };

        return Ok(new
        {
            ok = true,
            modulo,
            version = ver
        });
    }
}
