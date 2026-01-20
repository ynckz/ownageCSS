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
        public override string ModuleVersion => "2.0";
        public override string ModuleAuthor => "You";
        public override string ModuleDescription => "Plays OWNAGE sound when landing on enemy head";

        private Dictionary<ulong, float> _lastOwnageTime = new();
        private const float COOLDOWN = 2.5f; // секунды
        private const string OWNAGE_SOUND_PATH = "ownage/ownage.mp3"; // Путь к нашему файлу

        public override void Load(bool hotReload)
        {
            AddTimer(0.1f, CheckForHeadLandings, TimerFlags.REPEAT);
            
            // Регистрация команд
            AddCommand("css_ownage_test", "Test the ownage system", CommandOwnageTest);
            AddCommand("css_ownage_sound", "Play ownage sound", CommandOwnageSound);
            AddCommand("css_ownage_reload", "Reload ownage configuration", CommandOwnageReload);
            AddCommand("css_ownage_debug", "Debug sound system", CommandOwnageDebug);
        }

        private void CheckForHeadLandings()
        {
            var players = Utilities.GetPlayers().Where(p => 
                p != null &&
                p.IsValid && 
                !p.IsBot && 
                p.Pawn.IsValid && 
                p.Pawn.Value != null && 
                p.Pawn.Value?.AbsOrigin != null).ToList();

            foreach (var jumper in players)
            {
                var jumperPos = jumper.Pawn.Value?.AbsOrigin;
                if (jumperPos == null) continue;

                foreach (var victim in players.Where(v => v != null && v.SteamID != jumper.SteamID))
                {
                    var victimPos = victim.Pawn.Value?.AbsOrigin;
                    if (victimPos == null) continue;

                    // Горизонтальное расстояние
                    float dist2d = MathF.Sqrt(
                        MathF.Pow(jumperPos.X - victimPos.X, 2) +
                        MathF.Pow(jumperPos.Y - victimPos.Y, 2)
                    );

                    // Высота головы жертвы
                    float victimHeadZ = victimPos.Z + 64.0f;
                    float heightDiff = jumperPos.Z - victimHeadZ;

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
            // Проигрываем звук всем валидным игрокам
            PlaySoundToAll(OWNAGE_SOUND_PATH);

            // Сообщение в чат
            Server.PrintToChatAll($" \x04[OWNAGE]\x01 {jumper.PlayerName} \x05заовнил\x01 {victim.PlayerName}!");
        }

        // Универсальный метод для проигрывания звука
        private void PlaySoundToPlayer(CCSPlayerController player, string soundPath)
        {
            if (player == null || !player.IsValid || player.IsBot || !player.Pawn.IsValid || player.Pawn.Value == null)
                return;

            try
            {
                // Правильный способ для MP3/WAV файлов
                player.ExecuteClientCommand($"play {soundPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OwnagePlugin] Error playing sound to {player.PlayerName}: {ex.Message}");
            }
        }

        private void PlaySoundToAll(string soundPath)
        {
            foreach (var player in Utilities.GetPlayers())
            {
                if (player == null) continue;
                PlaySoundToPlayer(player, soundPath);
            }
        }

        // Вспомогательный метод для поиска игрока по имени
        private CCSPlayerController? FindPlayerByName(string playerName)
        {
            playerName = playerName.ToLower().Trim();
            
            // Сначала ищем по точному совпадению
            foreach (var player in Utilities.GetPlayers())
            {
                if (player == null || !player.IsValid || player.IsBot) continue;
                
                if (player.PlayerName.ToLower().Trim() == playerName)
                    return player;
            }
            
            // Потом по частичному совпадению
            foreach (var player in Utilities.GetPlayers())
            {
                if (player == null || !player.IsValid || player.IsBot) continue;
                
                if (player.PlayerName.ToLower().Trim().Contains(playerName))
                    return player;
            }
            
            return null;
        }

        // Команда для тестирования системы ownage
        [CommandHelper(minArgs: 0, usage: "[target]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        public void CommandOwnageTest(CCSPlayerController? caller, CommandInfo command)
        {
            try
            {
                // Если команда вызвана с серверной консоли
                if (caller == null)
                {
                    var players = Utilities.GetPlayers().Where(p => p != null && p.IsValid && !p.IsBot).ToList();
                    if (players.Count < 2)
                    {
                        command.ReplyToCommand("❌ Not enough players to test ownage! Need at least 2 players.");
                        return;
                    }

                    var jumper = players[0];
                    var victim = players[1];
                    TriggerOwnage(jumper, victim);
                    command.ReplyToCommand($"✅ Test ownage triggered between {jumper.PlayerName} and {victim.PlayerName}");
                    return;
                }

                // Если команда вызвана игроком
                if (command.ArgCount >= 2)
                {
                    var targetName = command.GetArg(1);
                    var target = FindPlayerByName(targetName);
                    
                    if (target == null || !target.IsValid || target.IsBot)
                    {
                        command.ReplyToCommand($"❌ Player '{targetName}' not found! Check name and try again.");
                        return;
                    }

                    if (caller.SteamID == target.SteamID)
                    {
                        command.ReplyToCommand("❌ You can't ownage yourself! Choose another player.");
                        return;
                    }

                    TriggerOwnage(caller, target);
                    command.ReplyToCommand($"✅ Test ownage triggered on {target.PlayerName}");
                }
                else
                {
                    var otherPlayers = Utilities.GetPlayers().Where(p => 
                        p != null && p.IsValid && !p.IsBot && p.SteamID != caller.SteamID).ToList();
                    
                    if (otherPlayers.Count == 0)
                    {
                        command.ReplyToCommand("❌ No other players to test on! Wait for someone to join.");
                        return;
                    }

                    var randomVictim = otherPlayers[new Random().Next(otherPlayers.Count)];
                    TriggerOwnage(caller, randomVictim);
                    command.ReplyToCommand($"✅ Test ownage triggered on {randomVictim.PlayerName}");
                }
            }
            catch (Exception ex)
            {
                command.ReplyToCommand($"❌ Error: {ex.Message}");
            }
        }

        // Команда для проигрывания звука
        [CommandHelper(minArgs: 0, usage: "[target/all]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        public void CommandOwnageSound(CCSPlayerController? caller, CommandInfo command)
        {
            try
            {
                string soundPath = OWNAGE_SOUND_PATH;
                
                // Если команда вызвана с серверной консоли
                if (caller == null)
                {
                    if (command.ArgCount >= 2)
                    {
                        var targetArg = command.GetArg(1);
                        
                        if (targetArg.Equals("all", StringComparison.OrdinalIgnoreCase))
                        {
                            PlaySoundToAll(soundPath);
                            command.ReplyToCommand("✅ Ownage sound played for all players");
                            return;
                        }
                        
                        var target = FindPlayerByName(targetArg);
                        if (target == null || !target.IsValid || target.IsBot)
                        {
                            command.ReplyToCommand($"❌ Player '{targetArg}' not found!");
                            return;
                        }
                        
                        PlaySoundToPlayer(target, soundPath);
                        command.ReplyToCommand($"✅ Ownage sound played for {target.PlayerName}");
                        return;
                    }
                    
                    command.ReplyToCommand("ℹ️ Usage: css_ownage_sound [player_name/all]");
                    return;
                }

                // Если команда вызвана игроком
                if (command.ArgCount >= 2)
                {
                    var targetArg = command.GetArg(1);
                    
                    if (targetArg.Equals("all", StringComparison.OrdinalIgnoreCase))
                    {
                        PlaySoundToAll(soundPath);
                        command.ReplyToCommand("✅ Ownage sound played for all players");
                    }
                    else
                    {
                        var target = FindPlayerByName(targetArg);
                        if (target == null || !target.IsValid || target.IsBot)
                        {
                            command.ReplyToCommand($"❌ Player '{targetArg}' not found!");
                            return;
                        }
                        
                        PlaySoundToPlayer(target, soundPath);
                        command.ReplyToCommand($"✅ Ownage sound played for {target.PlayerName}");
                    }
                }
                else
                {
                    // Проиграть звук только для вызвавшего игрока
                    PlaySoundToPlayer(caller, soundPath);
                    command.ReplyToCommand("✅ Ownage sound played for you");
                }
            }
            catch (Exception ex)
            {
                command.ReplyToCommand($"❌ Error: {ex.Message}");
            }
        }

        // Команда для перезагрузки конфигурации
        [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.SERVER_ONLY)]
        public void CommandOwnageReload(CCSPlayerController? caller, CommandInfo command)
        {
            _lastOwnageTime.Clear();
            command.ReplyToCommand("✅ Ownage plugin reloaded successfully!");
        }

        // Команда для отладки звуковой системы
        [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        public void CommandOwnageDebug(CCSPlayerController? caller, CommandInfo command)
        {
            try
            {
                command.ReplyToCommand("🔍 Ownage Plugin Debug Info:");
                command.ReplyToCommand($"- Sound File: '{OWNAGE_SOUND_PATH}'");
                command.ReplyToCommand($"- Total Players: {Utilities.GetPlayers().Count(p => p != null && p.IsValid)}");
                
                if (caller != null && caller.IsValid)
                {
                    command.ReplyToCommand($"- Your Position: {caller.Pawn.Value?.AbsOrigin?.ToString() ?? "N/A"}");
                    command.ReplyToCommand($"- Test Sound: Playing to you only...");
                    PlaySoundToPlayer(caller, OWNAGE_SOUND_PATH);
                }
                
                command.ReplyToCommand("✅ Debug complete!");
            }
            catch (Exception ex)
            {
                command.ReplyToCommand($"❌ Debug Error: {ex.Message}");
            }
        }
    }
}