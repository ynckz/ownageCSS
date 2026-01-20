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
        public override string ModuleVersion => "2.1";
        public override string ModuleAuthor => "You";
        public override string ModuleDescription => "Plays OWNAGE sound when landing on enemy head";

        private Dictionary<ulong, float> _lastOwnageTime = new();
        private const float COOLDOWN = 2.5f;
        
        // ПРАВИЛЬНЫЙ ПУТЬ ДЛЯ CS2:
        private const string OWNAGE_SOUND_PATH = "ownage/ownage.wav"; // Именно так!

        public override void Load(bool hotReload)
        {
            AddTimer(0.1f, CheckForHeadLandings, TimerFlags.REPEAT);
            
            // Регистрация команд
            AddCommand("css_ownage_test", "Test the ownage system", CommandOwnageTest);
            AddCommand("css_ownage_sound", "Play ownage sound", CommandOwnageSound);
            AddCommand("css_ownage_debug", "Debug sound path", CommandOwnageDebug);
        }

        private void CheckForHeadLandings()
        {
            var players = Utilities.GetPlayers().Where(p => 
                p != null && p.IsValid && !p.IsBot && 
                p.Pawn.IsValid && p.Pawn.Value != null && 
                p.Pawn.Value?.AbsOrigin != null).ToList();

            foreach (var jumper in players)
            {
                var jumperPos = jumper.Pawn.Value?.AbsOrigin;
                if (jumperPos == null) continue;

                foreach (var victim in players.Where(v => v != null && v.SteamID != jumper.SteamID))
                {
                    var victimPos = victim.Pawn.Value?.AbsOrigin;
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
            PlaySoundToAll(OWNAGE_SOUND_PATH);
            Server.PrintToChatAll($" \x04[OWNAGE]\x01 {jumper.PlayerName} \x05заовнил\x01 {victim.PlayerName}!");
        }

        private void PlaySoundToPlayer(CCSPlayerController player, string soundPath)
        {
            if (player == null || !player.IsValid || player.IsBot || !player.Pawn.IsValid || player.Pawn.Value == null)
                return;

            // ПРОСТОЙ И РАБОЧИЙ СПОСОБ:
            player.ExecuteClientCommand($"play {soundPath}");
        }

        private void PlaySoundToAll(string soundPath)
        {
            foreach (var player in Utilities.GetPlayers())
            {
                if (player == null) continue;
                PlaySoundToPlayer(player, soundPath);
            }
        }

        private CCSPlayerController? FindPlayerByName(string playerName)
        {
            playerName = playerName.ToLower().Trim();
            
            foreach (var player in Utilities.GetPlayers())
            {
                if (player == null || !player.IsValid || player.IsBot) continue;
                if (player.PlayerName.ToLower().Trim().Contains(playerName))
                    return player;
            }
            return null;
        }

        [CommandHelper(minArgs: 0, usage: "[target]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        public void CommandOwnageTest(CCSPlayerController? caller, CommandInfo command)
        {
            if (caller == null) // серверная консоль
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
                var randomVictim = Utilities.GetPlayers().Where(p => 
                    p != null && p.IsValid && !p.IsBot && p.SteamID != caller.SteamID)
                    .FirstOrDefault();
                
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
                PlaySoundToAll(OWNAGE_SOUND_PATH);
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
                PlaySoundToPlayer(target, OWNAGE_SOUND_PATH);
                command.ReplyToCommand($"✅ Звук OWNAGE для {target.PlayerName}!");
            }
            else
            {
                PlaySoundToPlayer(caller!, OWNAGE_SOUND_PATH);
                command.ReplyToCommand("✅ Звук OWNAGE для тебя!");
            }
        }

        [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        public void CommandOwnageDebug(CCSPlayerController? caller, CommandInfo command)
        {
            command.ReplyToCommand($"🔍 OWNAGE DEBUG:");
            command.ReplyToCommand($"- Путь к звуку: '{OWNAGE_SOUND_PATH}'");
            command.ReplyToCommand($"- Файл должен лежать в: /csgo/sound/{OWNAGE_SOUND_PATH}");
            command.ReplyToCommand($"- Версия API: {ApiVersion}");
            
            if (caller != null)
            {
                command.ReplyToCommand($"- Твоя позиция: {caller.Pawn.Value?.AbsOrigin?.ToString() ?? "N/A"}");
            }
            
            command.ReplyToCommand($"✅ Чтобы проверить звук: css_ownage_sound");
        }
    }
}