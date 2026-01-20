using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using System.Collections.Generic;
using System.Linq;
using CounterStrikeSharp.API.Modules.Entities;

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
            var players = Utilities.GetPlayers().Where(p => 
                p.IsValid && 
                !p.IsBot && 
                p.Pawn.IsValid && 
                p.Pawn.Value != null && 
                p.Pawn.Value.AbsOrigin != null).ToList();

            foreach (var jumper in players)
            {
                var jumperPos = jumper.Pawn.Value.AbsOrigin;
                if (jumperPos == null) continue;

                foreach (var victim in players.Where(v => v.SteamID != jumper.SteamID))
                {
                    var victimPos = victim.Pawn.Value.AbsOrigin;
                    if (victimPos == null) continue;

                    // Горизонтальное расстояние
                    float dist2d = MathF.Sqrt(
                        MathF.Pow(jumperPos!.X - victimPos!.X, 2) +
                        MathF.Pow(jumperPos!.Y - victimPos!.Y, 2)
                    );

                    // Высота головы жертвы
                    float victimHeadZ = victimPos!.Z + 64.0f;
                    float heightDiff = jumperPos!.Z - victimHeadZ;

                    // Условия активации
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

            // Проигрываем звук всем валидным игрокам
            foreach (var player in Utilities.GetPlayers())
            {
                if (player == null || 
                    !player.IsValid || 
                    player.IsBot || 
                    !player.Pawn.IsValid || 
                    player.Pawn.Value == null) 
                {
                    continue;
                }

                player.ExecuteClientCommand($"play {soundEvent}");
            }

            // Сообщение в чат
            Server.PrintToChatAll($" \x04[OWNAGE]\x01 {jumper.PlayerName} \x05заовнил\x01 {victim.PlayerName}!");
        }
    }
}