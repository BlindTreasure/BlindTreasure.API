using BlindTreasure.Domain.DTOs.ShipmentDTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers
{
    [ApiController]
    [Route("/services/shipment")]

    public class ShipmentServiceController : ControllerBase
    {

        
        [HttpPost("order")]
        [Authorize(AuthenticationSchemes = "Bearer,X-Client-Source")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]

        public IActionResult CreateShipment([FromBody] SubmitOrderRequestOrder shipmentDto)
        {

            return Ok();
        }
    }
}
