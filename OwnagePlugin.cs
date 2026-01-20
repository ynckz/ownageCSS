using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using System.Collections.Generic;
using System.Linq;

namespace OwnagePlugin
{
    public class OwnagePlugin : BasePlugin
    {
        public override string ModuleName => "Ownage Headstomp";
        public override string ModuleVersion => "1.0";
        public override string ModuleAuthor => "You";
        public override string ModuleDescription => "Plays QuakeSoundsD.Ownage when landing on enemy head";

        private Dictionary<ulong, float> _lastOwnageTime = new();
        private const float COOLDOWN = 2.5f; // секунды

        public override void Load(bool hotReload)
        {
            AddTimer(0.1f, CheckForHeadLandings, TimerFlags.REPEAT);
        }

        private void CheckForHeadLandings()
        {
            var players = Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot && p.Pawn.Value != null).ToList();

            foreach (var jumper in players)
            {
                var jumperPawn = jumper.Pawn.Value;
                if (jumperPawn == null) continue;

                var jumperPos = jumperPawn.AbsOrigin;
                if (jumperPos == null) continue;

                foreach (var victim in players.Where(v => v.SteamID != jumper.SteamID))
                {
                    var victimPawn = victim.Pawn.Value;
                    if (victimPawn == null) continue;

                    var victimPos = victimPawn.AbsOrigin;
                    if (victimPos == null) continue;

                    // Горизонтальное расстояние (радиус модели игрока ~24-32)
                    float dist2d = MathF.Sqrt(
                        MathF.Pow(jumperPos.X - victimPos.X, 2) +
                        MathF.Pow(jumperPos.Y - victimPos.Y, 2)
                    );

                    // Высота головы жертвы (~64 units от origin)
                    float victimHeadZ = victimPos.Z + 64.0f;
                    float heightDiff = jumperPos.Z - victimHeadZ;

                    // Условия: близко по горизонтали, сверху, но не слишком высоко
                    if (dist2d < 32.0f && heightDiff > 5.0f && heightDiff < 120.0f)
                    {
                        if (!_lastOwnageTime.TryGetValue(jumper.SteamID, out float lastTime) ||
                            Server.CurrentTime - lastTime > COOLDOWN)
                        {
                            _lastOwnageTime[jumper.SteamID] = Server.CurrentTime;
                            TriggerOwnage(jumper, victim);
                        }
                    }
                }
            }
        }

        private void TriggerOwnage(CCSPlayerController jumper, CCSPlayerController victim)
        {
            string soundEvent = "QuakeSoundsD.Ownage";

            // Проигрываем звук ВСЕМ игрокам
            foreach (var player in Utilities.GetPlayers())
            {
                if (player?.IsValid == true && !player.IsBot)
                {
                    Utilities.EmitSound(player, soundEvent);
                }
            }

            // Сообщение в чат
            Server.PrintToChatAll($" \x04[OWNAGE]\x01 {jumper.PlayerName} \x05заовнил\x01 {victim.PlayerName}!");
        }
    }
}