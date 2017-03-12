using System;
using System.Drawing;
using System.Linq;
using GTA;
using NativeUI;

namespace GTACoOp
{
    public class DebugWindow
    {
        public bool Visible { get; set; }
        public int PlayerIndex { get; set; }

        public void Draw()
        {
            if (!Visible) return;

            if (Game.IsControlJustPressed(0, Control.FrontendLeft))
            {
                PlayerIndex--;
            }

            else if (Game.IsControlJustPressed(0, Control.FrontendRight))
            {
                PlayerIndex++;
            }

            if (PlayerIndex >= Main.Opponents.Count || PlayerIndex < 0)
            {
                // wrong index
                return;
            }

            var player = Main.Opponents.ElementAt(PlayerIndex);
            string output = "=======PLAYER #" + PlayerIndex + " INFO=======\n";
            output += "Name: " + player.Value.Name + "\n";
            output += "UID: " + player.Key + "\n";
            output += "IsInVehicle: " + player.Value.IsInVehicle + "\n";
            output += "Position: " + player.Value.Position + "\n";
            output += "VehiclePosition: " + player.Value.VehiclePosition + "\n";
            output += "VehModel: " + player.Value.VehicleHash + "\n";
            output += "Last Updated: " + player.Value.LastUpdateReceived + "\n";
            output += "Latency: " + player.Value.Latency + "\n";
            if (player.Value.Character != null) {
                output += "Character Pos: " + player.Value.Character.Position + "\n";
                output += "CharacterIsInVeh: " + player.Value.Character.IsInVehicle() + "\n";
                if (player.Value.Character.CurrentVehicle != null)
                    output += "Char Speed: " + player.Value.Character.CurrentVehicle.Speed + "\n";
            }
            output += "Net Speed: " + player.Value.Speed + "\n";

            
            new UIResText(output, new Point(500, 10), 0.5f) {Outline = true}.Draw();
        }
    }
}