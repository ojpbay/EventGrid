using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using eventGridViewer.Hubs;
using eventGridViewer.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace eventGridViewer.Controllers
{
    [Route("api/[controller]")]
    public class UpdatesController : Controller
    {
        private IHubContext<GridEventsHub> HubContext;
        private bool EventTypeSubcriptionValidation
            => HttpContext.Request.Headers["aeg-event-type"].FirstOrDefault() ==
                "SubscriptionValidation";

        private bool EventTypeNotification
            => HttpContext.Request.Headers["aeg-event-type"].FirstOrDefault() ==
               "Notification";
        public UpdatesController(IHubContext<GridEventsHub> gridEventsHubContext)
        {
            this.HubContext = gridEventsHubContext;
        }

        [HttpPost]
        public async Task<IActionResult> Post()
        {
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            {
                var jsonContent = await reader.ReadToEndAsync();

                // Check the event type.
                // Return the validation code if it's 
                // a subscription validation request. 
                if (EventTypeSubcriptionValidation)
                {
                    var gridEvent =
                        JsonConvert.DeserializeObject<List<GridEvent<Dictionary<string, string>>>>(jsonContent)
                            .First();

                    await this.HubContext.Clients.All.SendAsync(
                        "gridupdate",
                        gridEvent.Id,
                        gridEvent.EventType,
                        gridEvent.Subject,
                        gridEvent.EventTime.ToLongTimeString(),
                        jsonContent.ToString());

                    // Retrieve the validation code and echo back.
                    var validationCode = gridEvent.Data["validationCode"];
                    return new JsonResult(new
                    {
                        validationResponse = validationCode
                    });
                }
                else if (EventTypeNotification)
                {
                    var events = JArray.Parse(jsonContent);
                    foreach (var e in events)
                    {
                        // Invoke a method on the clients for 
                        // an event grid notiification.                        
                        var details = JsonConvert.DeserializeObject<GridEvent<dynamic>>(e.ToString());
                        await this.HubContext.Clients.All.SendAsync(
                            "gridupdate",
                            details.Id,
                            details.EventType,
                            details.Subject,
                            details.EventTime.ToLongTimeString(),
                            e.ToString());
                    }

                    return Ok();
                }
                else
                {
                    return BadRequest();
                }
            }

        }
    }
}