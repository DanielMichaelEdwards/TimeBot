using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using System;
using Microsoft.Bot.Builder.Luis;
using Microsoft.Bot.Builder.Luis.Models;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Configuration;
using System.Data;
using System.IO;

namespace TimeBot
{
    [BotAuthentication]
    public class MessagesController : ApiController
    {
        /// <summary>
        /// POST: api/Messages
        /// Receive a message from a user and reply to it
        /// </summary>
        /// 
        string connectionString;
        bool exisiting = false;
        public async Task<HttpResponseMessage> Post([FromBody]Activity activity)
        {

            
            if (activity.Type == ActivityTypes.Message)
            {

                StateClient stateClient = activity.GetStateClient();
                BotData userData = stateClient.BotState.GetPrivateConversationData(activity.ChannelId, activity.Conversation.Id, activity.From.Id);
                await Conversation.SendAsync(activity, () => new GetTimeDialog());
            }
            else
            {
                HandleSystemMessage(activity);
            }
            var response = Request.CreateResponse(HttpStatusCode.OK);
            return response;
        }

        private Activity HandleSystemMessage(Activity message)
        {
            if (message.Type == ActivityTypes.DeleteUserData)
            {
                // Implement user deletion here
                // If we handle user deletion, return a real message
            }
            else if (message.Type == ActivityTypes.ConversationUpdate)
            {
                // Handle conversation state changes, like members being added and removed
                // Use Activity.MembersAdded and Activity.MembersRemoved and Activity.Action for info
                // Not available in all channels
            }
            else if (message.Type == ActivityTypes.ContactRelationUpdate)
            {
                // Handle add/remove from contact lists
                // Activity.From + Activity.Action represent what happened
            }
            else if (message.Type == ActivityTypes.Typing)
            {
                // Handle knowing tha the user is typing
            }
            else if (message.Type == ActivityTypes.Ping)
            {
            }

            return null;
        }
    }
    [LuisModel("fa643f4b-d76d-4b61-bd2e-cf85d520b7b8", "137fcf0303804aa5a0a727186a577d8d", domain: "westus.api.cognitive.microsoft.com")]
    [Serializable]
    public class GetTimeDialog: LuisDialog<object>
    {
        private readonly Dictionary<string, Event> eventByTitle = new Dictionary<string, Event>();
        //Careful - "event" is a reserved word
        [Serializable]
        public sealed class Event : IEquatable<Event>
        {
            public string Name { get; set; }
            public string Time { get; set; }

            public override string ToString()
            {
                return $"[{this.Name} is at {this.Time}]";
            }

            public bool Equals(Event other)
            {
                return other != null && this.Name == other.Name && this.Time == other.Time;
            }

            public override bool Equals(object other)
            {
                return Equals(other as Event);
            }
            public override int GetHashCode()
            {
                return this.Name.GetHashCode();
            }
        }

        //CONSTANTS
        public const string Entity_Event_Name = "Event.Name";
        public const string Entity_Event_DateTime = "builtin.datetimeV2.time";
        public const string DefaultEventName = "default";

        
        //Greeting
        [LuisIntent("Greeting")]
        public async Task GreetingIntent(IDialogContext context, LuisResult result)
        {
            string username;
            if (context.PrivateConversationData.TryGetValue<string>("Username", out username))
            {
                await context.PostAsync($"Hi {username}, welcome back!");
            }
            else
            {
                PromptDialog.Text(context, After_UsernamePrompt, "Hi there! I didn't get your name, what shall I call you?");
            }        

        }

        private async Task After_UsernamePrompt(IDialogContext context, IAwaitable<string> result)
        {
            string username = await result;
            context.PrivateConversationData.SetValue<string>("Username", username);
            await context.PostAsync($"Pleasure to meet you {username}.");
            await context.PostAsync("What can I do for you?");
            context.Wait(MessageReceived);
        }

        //Farewell
        [LuisIntent("Farewell")]
        public async Task FarewellIntent(IDialogContext context, LuisResult result)
        {
            string username;
            if (context.PrivateConversationData.TryGetValue<string>("Username", out username))
            {
                await context.PostAsync($"Until we meet again {username}, take care.");
            }
            else
            {
                await context.PostAsync("Until we meet again stranger, take care.");
            }
            
        }

        //Get the time
        [LuisIntent("Time.GetTime")]
        public async Task GetTimeIntent(IDialogContext context, LuisResult result)
        {
            TimeRetriver timeRetriver = new TimeRetriver();
            await context.PostAsync("The current time is: " + timeRetriver.ToString());
            context.Wait(MessageReceived);
            
        }

        private Event eventToCreate;
        private string currentName;
        private bool existingTime;
        private bool existingName;
        [LuisIntent("Event.Create")]
        public Task CreateEventIntent(IDialogContext context, LuisResult result)
        {
            

            //Find the name of the event
            EntityRecommendation name;
            EntityRecommendation time;
            existingTime = false;
            existingName = false;
            if((!result.TryFindEntity(Entity_Event_Name, out name)) && (!result.TryFindEntity(Entity_Event_DateTime, out time)))
            {
                PromptDialog.Text(context, After_NamePrompt, "What would you like to call your event?");
            }
            else if ((result.TryFindEntity(Entity_Event_Name, out name)) && (!result.TryFindEntity(Entity_Event_DateTime, out time)))
            {
                //Create the new event object
                var newEvent = new Event() { Name = name.Entity };
                //Add the new event to the list of events and also save it in order to add content to it later
                eventToCreate = this.eventByTitle[newEvent.Name] = newEvent;
                existingName = true;
                //Ask the user what time they want the event to happen
                PromptDialog.Text(context, After_TimePrompt, "What time would you like to schedule this event?");                  
            }
            else if ((!result.TryFindEntity(Entity_Event_Name, out name)) && (result.TryFindEntity(Entity_Event_DateTime, out time)))
            {
                name = new EntityRecommendation(type: Entity_Event_Name) { Entity = DefaultEventName };
                //Create the new event object
                var newEvent = new Event() {Name = name.Entity, Time = time.Entity };
                //Add the new event to the list of events and also save it in order to add content to it later
                eventToCreate = this.eventByTitle[newEvent.Name] = newEvent;
                existingTime = true;
                PromptDialog.Text(context, After_NamePrompt, "What would you like to call this event?");
            }
            else if ((result.TryFindEntity(Entity_Event_Name, out name)) && (result.TryFindEntity(Entity_Event_DateTime, out time)))
            {
                //Create the new event object
                var newEvent = new Event() { Name = name.Entity, Time = time.Entity };
                //Add the new event to the list of events and also save it in order to add content to it later
                eventToCreate = this.eventByTitle[newEvent.Name] = newEvent;
                existingTime = true;
                existingName = true;
                DisplayEvent(context);
            }     
            return Task.CompletedTask;
        }
        //This task deals with adding the time to the event
        private async Task After_NamePrompt(IDialogContext context, IAwaitable<string> result)
        {
            EntityRecommendation name;
            //Set the title (used for creation, deletion and reading back)
            currentName = await result;
            if ((currentName != null) || (existingName == false))
            {
                name = new EntityRecommendation(type: Entity_Event_Name) { Entity = currentName };
            }
            else
            {
                //Use the default
                name = new EntityRecommendation(type: Entity_Event_Name) { Entity = DefaultEventName };
            }

            if (!existingTime)
            {

                if (!existingName)
                {
                    var updatedEvent = new Event(){Name = name.Entity };
                    eventToCreate = this.eventByTitle[updatedEvent.Name] = updatedEvent;
                }
                else
                {
                    //Create the new event object
                    var newEvent = new Event() { Name = name.Entity };
                    //Add the new event to the list of events and also save it in order to add content to it later
                    eventToCreate = this.eventByTitle[newEvent.Name] = newEvent;
                }
                

                //Ask the user when they want to schedule the event.
                PromptDialog.Text(context, After_TimePrompt, "When would you like to schedule this event?");
            }
            else
            {
                await DisplayEvent(context);
            }     
                  
        }    

        private async Task After_TimePrompt(IDialogContext context, IAwaitable<string> result)
        {
            //Set the time of the event           
            eventToCreate.Time = await result;

            await DisplayEvent(context);
        }

        private async Task DisplayEvent(IDialogContext context)
        {
            string username;
            if (context.PrivateConversationData.TryGetValue<string>("Username", out username))
            {
                await context.PostAsync($"Ok {username}, I've created the event **{this.eventToCreate.Name}** which is scheduled at {this.eventToCreate.Time}.");
            }
            else
            {
                await context.PostAsync($"Created event **{this.eventToCreate.Name}** which is scheduled at {this.eventToCreate.Time}");
            }            
            context.Wait(MessageReceived);
        }

        //Thank you
        [LuisIntent("ThankYou")]
        public async Task ThankYouIntent(IDialogContext context, LuisResult result)
        {
            string username;
            if (context.PrivateConversationData.TryGetValue<string>("Username", out username))
            {
                await context.PostAsync($"Always happy to help {username}!");
            }
            else
            {
                await context.PostAsync("Always happy to help!");

            }
            
            context.Wait(MessageReceived);
        }

        //List all events
        [LuisIntent("Event.GetAll")]
        public async Task GetAllEventsIntent(IDialogContext context, LuisResult result)
        {

            if (eventByTitle.Count < 1)
            {
                await context.PostAsync("There are currently no events scheduled. Ask me to create one for you!");                
            }
            else
            {
                string EventList = "Here's the list of all your events: \n\n";
                foreach (KeyValuePair<string, Event> entry in eventByTitle)
                {
                    Event eventInList = entry.Value;
                    EventList += $"**{eventInList.Name}** at {eventInList.Time}.\n\n";
                }
                await context.PostAsync(EventList);
            }           
            context.Wait(MessageReceived);
        }

        //Fetches a specific event
        [LuisIntent("Event.Get")]
        public async Task GetEventIntent(IDialogContext context, LuisResult result)
        {
            Event foundEvent;
            if (TryFindEvent(result, out foundEvent))
            {
                await context.PostAsync($"The event **{foundEvent.Name}** is scheduled for {foundEvent.Time}");
            }
            else if (eventByTitle.Count >= 1)
            {
                await context.PostAsync("Sorry, I couldn't find the event you asked for... Please check the spelling and try again.");
            }
            else
            {
                await context.PostAsync("There aren't any events scheduled. Create an event and then ask me again! :)");
            }
            context.Wait(MessageReceived);
        }

        //Deletes an event
        [LuisIntent("Event.Delete")]
        public async Task DeleteEventIntent(IDialogContext context, LuisResult result)
        {
            Event eventToDelete;
            if (eventByTitle.Count <1)
            {
                await context.PostAsync("I don't have any events to delete. Ask me to create one :)");
                context.Wait(MessageReceived);
            }
            else if (TryFindEvent(result, out eventToDelete))
            {
                this.eventByTitle.Remove(eventToDelete.Name);
                await context.PostAsync($"Event **{eventToDelete.Name}** deleted.");
            }
            else
            {
                //If you can find the name of the event, ask the user for it
                PromptDialog.Text(context, After_DeleteTitlePrompt, "What is the name of the event you want to delete?");
            }
        }

        private async Task After_DeleteTitlePrompt(IDialogContext context, IAwaitable<string> result)
        {
            Event eventToDelete;
            string nameToDelete = await result;
            bool foundEvent = this.eventByTitle.TryGetValue(nameToDelete, out eventToDelete);

            if (foundEvent)
            {
                this.eventByTitle.Remove(eventToDelete.Name);
                await context.PostAsync($"Event **{eventToDelete.Name}** deleted.");
            }
            else
            {
                await context.PostAsync($"Sorry, I didn't find the event name {nameToDelete}. Please check the spelling and try again :)");
            }

            context.Wait(MessageReceived);
        }

        //Help
        [LuisIntent("OnDevice.Help")]
        public async Task HelpIntent(IDialogContext context, LuisResult result)
        {
            await context.PostAsync($"Here's a list of things I can do: \n\n" +
                "- Create an event for you \n\n" +
                "- Tell you what time an event is scheduled to happen \n\n" +
                "- Show you a list of all the events you have scheduled \n\n" +
                "- Delete an event for you \n\n" +
                "- Tell you what the time is \n\n\n" + 
                "What can I do for you? :)");

            context.Wait(MessageReceived);
        }


        public bool TryFindEvent(string eventName, out Event retrivedEvent)
        {
            bool foundEvent = this.eventByTitle.TryGetValue(eventName, out retrivedEvent);
            return foundEvent;
        }

        public bool TryFindEvent(LuisResult result, out Event retrivedEvent)
        {
            retrivedEvent = null;
            string nameToFind;

            EntityRecommendation name;
            if (result.TryFindEntity(Entity_Event_Name, out name))
            {
                nameToFind = name.Entity;
            }
            else
            {
                nameToFind = DefaultEventName;
            }

            return this.eventByTitle.TryGetValue(nameToFind, out retrivedEvent);//Returns false if no event is found.
        }

        [LuisIntent("")]
        public async Task None(IDialogContext context, LuisResult result)
        {
            
            string message = "Sorry, I didn't get that. Try again or ask me for help.";
            await context.PostAsync(message);
            context.Wait(MessageReceived);
        }        
        
        
    }

}