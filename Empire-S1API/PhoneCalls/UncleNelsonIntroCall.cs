using S1API.Entities;
using S1API.Entities.NPCs;
using S1API.PhoneCalls;

namespace Empire.PhoneCalls
{
    // Phone call that delivers Uncle Nelson's intro dialogue as a staged call
    public class UncleNelsonIntroCall : PhoneCallDefinition
    {
        public UncleNelsonIntroCall(NPC? caller) : base(caller)
        {
            // Mirror the three intro text messages as call stages
            AddStage("Listen up, kid. I'm out of the business now, but I still got some connections. I'll put in a good word for you. How far you go is up to you. Make good relations and do good business with the dealers and you'll climb up the ladders. That'll get you access to some top tier deals.");
            AddStage("But I owed some cash to the wrong people. Cartels and the wrong sort. They'll expect you to pay it off since you're the man of the family now. They not the negotiating type, if you know what I mean. That means you need to step up son. Don't let me down. I don't want to see you in a body bag. You got that?");
            AddStage("I'll help you out however I can. Don't come looking for me though. Heh. I'll call from time to time. Take care son.");

            Completed();
        }
    }
}
