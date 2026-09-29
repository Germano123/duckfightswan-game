using UnityEngine;
using System.Collections.Generic;
using System.IO;
using DuckFightSwan.Units;

namespace DuckFightSwan.Terrain
{
    [System.Serializable]
    public class TileSaveData
    {
        public int x;
        public int z;
        public int height;
        public TerrainType type;
    }

    [System.Serializable]
    public class UnitSaveData
    {
        public FactionType faction;
        public string className;
        public int x;
        public int z;
        public int currentHealth;
        public int level = 1;
        public int xp = 0;
    }

    [System.Serializable]
    public class LevelSaveData
    {
        public int width;
        public int depth;
        public float heightStep;
        public List<TileSaveData> tiles = new List<TileSaveData>();
        public List<UnitSaveData> units = new List<UnitSaveData>();
    }

    [System.Serializable]
    public class PlayerProfileData
    {
        public int currentPhase = 1;
        public int coins = 100;
    }

    /// <summary>
    /// Gerencia o salvamento e carregamento persistente do nível (grid e unidades) em formato JSON.
    /// Respeita o SRP ao conter exclusivamente lógica de I/O de dados de salvamento.
    /// </summary>
    public static class LevelDataManager
    {
        private static string ProfilePath => Path.Combine(Application.dataPath, "player_profile.json");

        private static string GetSavePathForPhase(int phase)
        {
            return Path.Combine(Application.dataPath, $"level_save_{phase}.json");
        }

        /// <summary>
        /// O salvamento e carregamento agora são garantidos para todas as fases.
        /// </summary>
        public static bool HasSaveFile(int phase)
        {
            return true;
        }

        /// <summary>
        /// Salva as informações do tabuleiro e das tropas ativas para a fase indicada.
        /// </summary>
        public static void SaveLevel(int phase, LevelSaveData data)
        {
            try
            {
                string json = JsonUtility.ToJson(data, true);
                string path = GetSavePathForPhase(phase);
                File.WriteAllText(path, json);
                Debug.Log($"[LevelDataManager] Nível da Fase {phase} salvo com sucesso em: {path}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LevelDataManager] Erro ao salvar nível da Fase {phase}: {ex.Message}");
            }
        }

        /// <summary>
        /// Carrega os dados de salvamento da fase indicada. Retorna e salva o design padrão/procedural se o arquivo não existir.
        /// </summary>
        public static LevelSaveData LoadLevel(int phase)
        {
            if (!File.Exists(GetSavePathForPhase(phase)))
            {
                Debug.Log($"[LevelDataManager] Arquivo de save para a Fase {phase} não encontrado. Criando design padrão ou procedural...");
                LevelSaveData defaultLevel = GenerateDefaultLevel(phase);
                SaveLevel(phase, defaultLevel);
                return defaultLevel;
            }

            try
            {
                string path = GetSavePathForPhase(phase);
                string json = File.ReadAllText(path);
                LevelSaveData data = JsonUtility.FromJson<LevelSaveData>(json);
                Debug.Log($"[LevelDataManager] Nível da Fase {phase} carregado com sucesso de: {path}");
                return data;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LevelDataManager] Erro ao carregar nível da Fase {phase}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Constrói a estrutura lógica e tropas para as fases. Fases 1 a 3 são curadas, fases superiores são procedurais.
        /// </summary>
        private static LevelSaveData GenerateDefaultLevel(int phase)
        {
            LevelSaveData data = new LevelSaveData();
            data.heightStep = 0.5f;

            if (phase == 1)
            {
                // Fase 1: Tutorial 10x10 planície suave
                data.width = 10;
                data.depth = 10;
                
                for (int x = 0; x < 10; x++)
                {
                    for (int z = 0; z < 10; z++)
                    {
                        int height = (x == 4 || x == 5) ? 1 : 0; // Pequeno relevo central
                        data.tiles.Add(new TileSaveData { x = x, z = z, height = height, type = TerrainType.Field });
                    }
                }

                // 2 Patos (Aliados) - Um Guerreiro e Um Arqueiro, conforme solicitação do usuário
                data.units.Add(new UnitSaveData { faction = FactionType.Ducks, className = "Warrior", x = 1, z = 3, currentHealth = 100 });
                data.units.Add(new UnitSaveData { faction = FactionType.Ducks, className = "Archer", x = 1, z = 6, currentHealth = 100 });

                // 2 Cisnes (Inimigos) - Um Guerreiro e Um Escudeiro
                data.units.Add(new UnitSaveData { faction = FactionType.Swans, className = "Warrior", x = 8, z = 3, currentHealth = 100 });
                data.units.Add(new UnitSaveData { faction = FactionType.Swans, className = "Squire", x = 8, z = 6, currentHealth = 100 });
            }
            else if (phase == 2)
            {
                // Fase 2: Colinas 12x12
                data.width = 12;
                data.depth = 12;

                for (int x = 0; x < 12; x++)
                {
                    for (int z = 0; z < 12; z++)
                    {
                        // Colina central de altura 2
                        int height = 0;
                        if (x >= 4 && x <= 7 && z >= 4 && z <= 7) height = 2;
                        else if (x >= 2 && x <= 9 && z >= 2 && z <= 9) height = 1;

                        TerrainType tType = height == 2 ? TerrainType.Forest : TerrainType.Field;
                        data.tiles.Add(new TileSaveData { x = x, z = z, height = height, type = tType });
                    }
                }

                // 3 Patos (Aliados) - Um de cada classe
                data.units.Add(new UnitSaveData { faction = FactionType.Ducks, className = "Warrior", x = 1, z = 2, currentHealth = 100 });
                data.units.Add(new UnitSaveData { faction = FactionType.Ducks, className = "Archer", x = 1, z = 6, currentHealth = 100 });
                data.units.Add(new UnitSaveData { faction = FactionType.Ducks, className = "Squire", x = 1, z = 9, currentHealth = 100 });

                // 3 Cisnes (Inimigos - um arquero na colina)
                data.units.Add(new UnitSaveData { faction = FactionType.Swans, className = "Warrior", x = 10, z = 3, currentHealth = 100 });
                data.units.Add(new UnitSaveData { faction = FactionType.Swans, className = "Archer", x = 9, z = 6, currentHealth = 100 });
                data.units.Add(new UnitSaveData { faction = FactionType.Swans, className = "Squire", x = 10, z = 8, currentHealth = 100 });
            }
            else if (phase == 3)
            {
                // Fase 3: Desfiladeiro/Gargalo 15x15
                data.width = 15;
                data.depth = 15;

                for (int x = 0; x < 15; x++)
                {
                    for (int z = 0; z < 15; z++)
                    {
                        // Gargalo vertical central (largura 3 de desfiladeiro, resto são montanhas de altura 3)
                        int height = 3;
                        if (z >= 6 && z <= 8)
                        {
                            height = 1;
                        }
                        else if (z == 5 || z == 9)
                        {
                            height = 2; // Rampa suave nas margens
                        }

                        TerrainType tType = height == 3 ? TerrainType.Mountain : TerrainType.Field;
                        data.tiles.Add(new TileSaveData { x = x, z = z, height = height, type = tType });
                    }
                }

                // 3 Patos (Aliados) - Um de cada classe
                data.units.Add(new UnitSaveData { faction = FactionType.Ducks, className = "Warrior", x = 2, z = 3, currentHealth = 100 });
                data.units.Add(new UnitSaveData { faction = FactionType.Ducks, className = "Archer", x = 2, z = 7, currentHealth = 100 });
                data.units.Add(new UnitSaveData { faction = FactionType.Ducks, className = "Squire", x = 2, z = 11, currentHealth = 100 });

                // 4 Cisnes (Inimigos - arqueiros na montanha defendendo a passagem)
                data.units.Add(new UnitSaveData { faction = FactionType.Swans, className = "Archer", x = 8, z = 3, currentHealth = 100 });
                data.units.Add(new UnitSaveData { faction = FactionType.Swans, className = "Warrior", x = 9, z = 7, currentHealth = 100 });
                data.units.Add(new UnitSaveData { faction = FactionType.Swans, className = "Squire", x = 10, z = 7, currentHealth = 100 });
                data.units.Add(new UnitSaveData { faction = FactionType.Swans, className = "Archer", x = 8, z = 11, currentHealth = 100 });
            }
            else
            {
                // Procedural fallback para fases maiores (> 3)
                data.width = 12;
                data.depth = 12;

                for (int x = 0; x < 12; x++)
                {
                    for (int z = 0; z < 12; z++)
                    {
                        int height = Random.Range(0, 3); // alturas 0, 1 ou 2
                        data.tiles.Add(new TileSaveData { x = x, z = z, height = height, type = TerrainType.Field });
                    }
                }

                // Spawna 3 Patos (Aliados) - Um de cada classe
                data.units.Add(new UnitSaveData { faction = FactionType.Ducks, className = "Warrior", x = 1, z = 2, currentHealth = 100 });
                data.units.Add(new UnitSaveData { faction = FactionType.Ducks, className = "Archer", x = 1, z = 6, currentHealth = 100 });
                data.units.Add(new UnitSaveData { faction = FactionType.Ducks, className = "Squire", x = 1, z = 9, currentHealth = 100 });

                // Spawna 3 Cisnes (Inimigos) - Um de cada classe
                data.units.Add(new UnitSaveData { faction = FactionType.Swans, className = "Warrior", x = 10, z = 3, currentHealth = 100 });
                data.units.Add(new UnitSaveData { faction = FactionType.Swans, className = "Archer", x = 10, z = 6, currentHealth = 100 });
                data.units.Add(new UnitSaveData { faction = FactionType.Swans, className = "Squire", x = 10, z = 8, currentHealth = 100 });
            }

            return data;
        }

        /// <summary>
        /// Salva o perfil geral de progressão do jogador.
        /// </summary>
        public static void SaveProfile(PlayerProfileData profile)
        {
            try
            {
                string json = JsonUtility.ToJson(profile, true);
                File.WriteAllText(ProfilePath, json);
                Debug.Log($"[LevelDataManager] Perfil de progresso do jogador salvo em: {ProfilePath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LevelDataManager] Erro ao salvar perfil do jogador: {ex.Message}");
            }
        }

        /// <summary>
        /// Carrega o perfil geral de progressão do jogador. Retorna novo perfil se não existir.
        /// </summary>
        public static PlayerProfileData LoadProfile()
        {
            if (!File.Exists(ProfilePath))
            {
                PlayerProfileData defaultProfile = new PlayerProfileData();
                SaveProfile(defaultProfile);
                return defaultProfile;
            }

            try
            {
                string json = File.ReadAllText(ProfilePath);
                PlayerProfileData profile = JsonUtility.FromJson<PlayerProfileData>(json);
                Debug.Log($"[LevelDataManager] Perfil do jogador carregado. Fase Atual: {profile.currentPhase} | Moedas: {profile.coins}");
                return profile;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LevelDataManager] Erro ao carregar perfil do jogador: {ex.Message}");
                return new PlayerProfileData();
            }
        }
    }
}
