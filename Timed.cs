namespace CrowdControl
{
    public class Timed
    {
        public TimedType type;
        float old;

        public Timed(TimedType t) { 
            type = t;
        }

        public void addEffect()
        {
            switch (type)
            {
                case TimedType.EXAMPLE:
                    {
                        
                        TestMod.ActionQueue.Enqueue(() =>
                        {
                            //add game code to run when the timed effect starts here
                        });
                        break;
                    }
            }
        }

        public void removeEffect()
        {
            switch (type)
            {
                case TimedType.EXAMPLE:
                    {
                        TestMod.ActionQueue.Enqueue(() =>
                        {
                            //add game code to run when the timed effect ends here
                        });
                        break;
                    }
            }
        }
        static int frames = 0;

        public void tick()
        {
            frames++;
            var playerRef = StartOfRound.Instance.localPlayerController;

            switch (type)
            {
                case TimedType.EXAMPLE:
                    //add game code to run every frame while the effect is active if needed
                    break;

            }
        }
    }
}
