using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using System.Collections.Generic;
using System.Linq;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Admin;

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
            
            // Регистрация команд
            AddCommand("css_ownage_test", "Test the ownage system", CommandOwnageTest);
            AddCommand("css_ownage_sound", "Play ownage sound", CommandOwnageSound);
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

        // Команда для тестирования системы ownage
        [CommandHelper(minArgs: 0, usage: "[target]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        public void CommandOwnageTest(CCSPlayerController? caller, CommandInfo command)
        {
            // Если команда вызвана с серверной консоли
            if (caller == null)
            {
                var players = Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot).ToList();
                if (players.Count < 2)
                {
                    command.ReplyToCommand("Not enough players to test ownage!");
                    return;
                }

                var jumper = players[0];
                var victim = players[1];
                TriggerOwnage(jumper, victim);
                command.ReplyToCommand($"Test ownage triggered between {jumper.PlayerName} and {victim.PlayerName}");
                return;
            }

            // Если команда вызвана игроком
            if (command.ArgCount >= 2)
            {
                var targetName = command.GetArg(1);
                var target = Utilities.GetPlayerFromUserInput(targetName);
                
                if (target == null || !target.IsValid || target.IsBot)
                {
                    command.ReplyToCommand($"Player {targetName} not found!");
                    return;
                }

                if (caller.SteamID == target.SteamID)
                {
                    command.ReplyToCommand("You can't ownage yourself!");
                    return;
                }

                TriggerOwnage(caller, target);
                command.ReplyToCommand($"Test ownage triggered on {target.PlayerName}");
            }
            else
            {
                var otherPlayers = Utilities.GetPlayers().Where(p => 
                    p.IsValid && !p.IsBot && p.SteamID != caller.SteamID).ToList();
                
                if (otherPlayers.Count == 0)
                {
                    command.ReplyToCommand("No other players to test on!");
                    return;
                }

                var randomVictim = otherPlayers[new Random().Next(otherPlayers.Count)];
                TriggerOwnage(caller, randomVictim);
                command.ReplyToCommand($"Test ownage triggered on {randomVictim.PlayerName}");
            }
        }

        // Команда для проигрывания звука
        [CommandHelper(minArgs: 0, usage: "[target/all]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        public void CommandOwnageSound(CCSPlayerController? caller, CommandInfo command)
        {
            string soundEvent = "QuakeSoundsD.Ownage";
            
            // Если команда вызвана с серверной консоли
            if (caller == null)
            {
                if (command.ArgCount >= 2 && command.GetArg(1).Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var player in Utilities.GetPlayers())
                    {
                        if (player.IsValid && !player.IsBot && player.Pawn.IsValid && player.Pawn.Value != null)
                        {
                            player.ExecuteClientCommand($"play {soundEvent}");
                        }
                    }
                    command.ReplyToCommand("Ownage sound played for all players");
                }
                else
                {
                    command.ReplyToCommand("Usage: css_ownage_sound [player_name/all]");
                }
                return;
            }

            // Если команда вызвана игроком
            if (command.ArgCount >= 2)
            {
                var targetArg = command.GetArg(1);
                
                if (targetArg.Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var player in Utilities.GetPlayers())
                    {
                        if (player.IsValid && !player.IsBot && player.Pawn.IsValid && player.Pawn.Value != null)
                        {
                            player.ExecuteClientCommand($"play {soundEvent}");
                        }
                    }
                    command.ReplyToCommand("Ownage sound played for all players");
                }
                else
                {
                    var target = Utilities.GetPlayerFromUserInput(targetArg);
                    if (target == null || !target.IsValid || target.IsBot)
                    {
                        command.ReplyToCommand($"Player {targetArg} not found!");
                        return;
                    }
                    
                    target.ExecuteClientCommand($"play {soundEvent}");
                    command.ReplyToCommand($"Ownage sound played for {target.PlayerName}");
                }
            }
            else
            {
                // Проиграть звук только для вызвавшего игрока
                caller.ExecuteClientCommand($"play {soundEvent}");
                command.ReplyToCommand("Ownage sound played for you");
            }
        }
    }
}