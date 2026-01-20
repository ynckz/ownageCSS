using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using System.Collections.Generic;
using System.Linq;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Commands;
using System;

namespace OwnagePlugin
{
    public class OwnagePlugin : BasePlugin
    {
        public override string ModuleName => "Ownage Headstomp";
        public override string ModuleVersion => "3.0";
        public override string ModuleAuthor => "You";
        public override string ModuleDescription => "Plays OWNAGE sound when landing on enemy head";
        
        // ИСПОЛЬЗУЕМ СОБЫТИЕ ИЗ НАШЕГО ФАЙЛА
        private const string OWNAGE_SOUND_EVENT = "Ownage.Sound";

        private Dictionary<ulong, float> _lastOwnageTime = new();
        private const float COOLDOWN = 2.5f;

        public override void Load(bool hotReload)
        {
            AddTimer(0.1f, CheckForHeadLandings, TimerFlags.REPEAT);
            AddCommand("css_ownage_test", "Test ownage system", CommandOwnageTest);
            AddCommand("css_ownage_sound", "Play ownage sound", CommandOwnageSound);
            AddCommand("css_ownage_debug", "Debug sound system", CommandOwnageDebug);
        }

        private void CheckForHeadLandings()
        {
            var players = Utilities.GetPlayers().Where(p => 
                p != null && p.IsValid && !p.IsBot && 
                p.Pawn.IsValid && p.Pawn.Value != null && 
                p.Pawn.Value.AbsOrigin != null).ToList();

            foreach (var jumper in players)
            {
                var jumperPos = jumper.Pawn.Value.AbsOrigin;
                if (jumperPos == null) continue;

                foreach (var victim in players.Where(v => v != null && v.SteamID != jumper.SteamID))
                {
                    var victimPos = victim.Pawn.Value.AbsOrigin;
                    if (victimPos == null) continue;

                    float dist2d = MathF.Sqrt(
                        MathF.Pow(jumperPos.X - victimPos.X, 2) +
                        MathF.Pow(jumperPos.Y - victimPos.Y, 2)
                    );

                    float victimHeadZ = victimPos.Z + 64.0f;
                    float heightDiff = jumperPos.Z - victimHeadZ;

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
            PlaySoundToAll(OWNAGE_SOUND_EVENT);
            Server.PrintToChatAll($" \x04[OWNAGE]\x01 {jumper.PlayerName} \x05заовнил\x01 {victim.PlayerName}!");
        }

        // ПРАВИЛЬНЫЙ СПОСОБ ДЛЯ SOUND EVENTS
        private void PlaySoundToPlayer(CCSPlayerController player, string soundEvent)
        {
            if (player == null || !player.IsValid || player.IsBot || !player.Pawn.IsValid)
                return;

            Utilities.EmitSound(player, soundEvent);
        }

        private void PlaySoundToAll(string soundEvent)
        {
            foreach (var player in Utilities.GetPlayers())
            {
                if (player == null) continue;
                PlaySoundToPlayer(player, soundEvent);
            }
        }

        private CCSPlayerController? FindPlayerByName(string playerName)
        {
            playerName = playerName.ToLower().Trim();
            return Utilities.GetPlayers()
                .FirstOrDefault(p => p != null && p.IsValid && !p.IsBot && 
                    p.PlayerName.ToLower().Contains(playerName));
        }

        [CommandHelper(minArgs: 0, usage: "[target]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        public void CommandOwnageTest(CCSPlayerController? caller, CommandInfo command)
        {
            if (caller == null) // консоль сервера
            {
                var players = Utilities.GetPlayers().Where(p => p != null && p.IsValid && !p.IsBot).ToList();
                if (players.Count < 2)
                {
                    command.ReplyToCommand("❌ Нужно минимум 2 игрока!");
                    return;
                }
                TriggerOwnage(players[0], players[1]);
                command.ReplyToCommand($"✅ OWNAGE между {players[0].PlayerName} и {players[1].PlayerName}!");
                return;
            }

            if (command.ArgCount >= 2)
            {
                var target = FindPlayerByName(command.GetArg(1));
                if (target == null)
                {
                    command.ReplyToCommand($"❌ Игрок не найден!");
                    return;
                }
                TriggerOwnage(caller, target);
                command.ReplyToCommand($"✅ OWNAGE на {target.PlayerName}!");
            }
            else
            {
                var randomVictim = Utilities.GetPlayers()
                    .FirstOrDefault(p => p != null && p.IsValid && !p.IsBot && p.SteamID != caller.SteamID);
                
                if (randomVictim == null)
                {
                    command.ReplyToCommand("❌ Нет других игроков!");
                    return;
                }
                
                TriggerOwnage(caller, randomVictim);
                command.ReplyToCommand($"✅ OWNAGE на {randomVictim.PlayerName}!");
            }
        }

        [CommandHelper(minArgs: 0, usage: "[all/player]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        public void CommandOwnageSound(CCSPlayerController? caller, CommandInfo command)
        {
            if (command.ArgCount >= 2 && command.GetArg(1).Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                PlaySoundToAll(OWNAGE_SOUND_EVENT);
                command.ReplyToCommand("✅ Звук OWNAGE для всех!");
            }
            else if (command.ArgCount >= 2)
            {
                var target = FindPlayerByName(command.GetArg(1));
                if (target == null)
                {
                    command.ReplyToCommand($"❌ Игрок не найден!");
                    return;
                }
                PlaySoundToPlayer(target, OWNAGE_SOUND_EVENT);
                command.ReplyToCommand($"✅ Звук OWNAGE для {target.PlayerName}!");
            }
            else
            {
                PlaySoundToPlayer(caller!, OWNAGE_SOUND_EVENT);
                command.ReplyToCommand("✅ Звук OWNAGE для тебя!");
            }
        }

        [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        public void CommandOwnageDebug(CCSPlayerController? caller, CommandInfo command)
        {
            command.ReplyToCommand($"🔍 OWNAGE DEBUG:");
            command.ReplyToCommand($"- Sound Event: '{OWNAGE_SOUND_EVENT}'");
            command.ReplyToCommand($"- Required Files:");
            command.ReplyToCommand($"  • soundevents/ownage/soundevents_ownage.vsndevts");
            command.ReplyToCommand($"  • sound/soundevents/ownage/ownage.vsnd_c");
            command.ReplyToCommand($"- Install Path: /csgo/");
            command.ReplyToCommand($"✅ To test: css_ownage_sound all");
        }
    }
}