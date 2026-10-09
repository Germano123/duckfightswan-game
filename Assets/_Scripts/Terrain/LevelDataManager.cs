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
        public int rank = 1;
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
        public static string DataDirectory => Path.Combine(Application.dataPath, "_Data");
        public static string LevelsDirectory => Path.Combine(DataDirectory, "Levels");
        public static string EpochsDirectory => Path.Combine(DataDirectory, "Epochs");
        public static string ProfilesDirectory => Path.Combine(DataDirectory, "Profiles");

        private static string ProfilePath
        {
            get
            {
                if (!Directory.Exists(ProfilesDirectory)) Directory.CreateDirectory(ProfilesDirectory);
                string newPath = Path.Combine(ProfilesDirectory, "player_profile.json");
                if (File.Exists(newPath)) return newPath;
                string legacyPath = Path.Combine(Application.dataPath, "player_profile.json");
                if (File.Exists(legacyPath)) return legacyPath;
                return newPath;
            }
        }

        public static string GetSavePathForPhase(int phase)
        {
            if (!Directory.Exists(LevelsDirectory)) Directory.CreateDirectory(LevelsDirectory);
            string newPath = Path.Combine(LevelsDirectory, $"level_save_{phase}.json");
            if (File.Exists(newPath)) return newPath;
            string legacyPath = Path.Combine(Application.dataPath, $"level_save_{phase}.json");
            if (File.Exists(legacyPath)) return legacyPath;
            return newPath;
        }

        public static string GetEpochPath(int year)
        {
            if (!Directory.Exists(EpochsDirectory)) Directory.CreateDirectory(EpochsDirectory);
            string newPath = Path.Combine(EpochsDirectory, $"level_save_epoch_{year}.json");
            if (File.Exists(newPath)) return newPath;
            string legacyPath = Path.Combine(Application.dataPath, $"level_save_epoch_{year}.json");
            if (File.Exists(legacyPath)) return legacyPath;
            return newPath;
        }

        /// <summary>
        /// Carrega os dados do snapshot de época do Cardinal (Ano 1, 15, 30, etc.).
        /// Possui fallback transparente para o arquivo de fase equivalente se o arquivo de época não existir.
        /// </summary>
        public static LevelSaveData LoadEpoch(int year)
        {
            string epochPath = GetEpochPath(year);
            if (File.Exists(epochPath))
            {
                try
                {
                    string json = File.ReadAllText(epochPath);
                    LevelSaveData data = JsonUtility.FromJson<LevelSaveData>(json);
                    Debug.Log($"[LevelDataManager] Época do Ano {year} carregada com sucesso de: {epochPath} ({data.tiles.Count} tiles).");
                    return data;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[LevelDataManager] Erro ao carregar época do Ano {year}: {ex.Message}");
                }
            }

            // Fallback por fase
            int phase = (year == 1) ? 1 : ((year == 15) ? 2 : 3);
            Debug.Log($"[LevelDataManager] level_save_epoch_{year}.json não encontrado. Tentando carregar fase {phase} como fallback...");
            return LoadLevel(phase);
        }

        /// <summary>
        /// Salva diretamente um snapshot de época (level_save_epoch_{year}.json).
        /// </summary>
        public static void SaveEpoch(int year, LevelSaveData data)
        {
            try
            {
                string path = GetEpochPath(year);
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(path, json);
                Debug.Log($"[LevelDataManager] Época do Ano {year} salva com sucesso em: {path}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LevelDataManager] Erro ao salvar época do Ano {year}: {ex.Message}");
            }
        }

        /// <summary>
        /// O salvamento e carregamento agora são garantidos para todas as fases.
        /// </summary>
        public static bool HasSaveFile(int phase)
        {
            return true;
        }

        /// <summary>
        /// Retorna o ano histórico/narrativo correspondente à época da simulação Cardinal na fase.
        /// </summary>
        public static int GetEpochYearForPhase(int phase)
        {
            switch (phase)
            {
                case 1: return 1;
                case 2: return 15;
                case 3: return 30;
                default: return 30 + (phase - 3) * 15;
            }
        }

        /// <summary>
        /// Retorna o nível recomendado para os gansos inimigos de acordo com a fase/ano para balancear o desafio.
        /// </summary>
        public static int GetEnemyLevelForPhase(int phase)
        {
            if (phase <= 1) return 1;
            if (phase == 2) return 3;
            if (phase == 3) return 5;
            return 1 + (phase - 1) * 2;
        }

        /// <summary>
        /// Retorna a patente militar recomendada para os gansos inimigos de acordo com a fase/ano.
        /// </summary>
        public static MilitaryRank GetEnemyRankForPhase(int phase)
        {
            if (phase <= 1) return MilitaryRank.Recruit;
            if (phase == 2) return MilitaryRank.Veteran;
            if (phase == 3) return MilitaryRank.Elite;
            return MilitaryRank.Commander;
        }

        /// <summary>
        /// Retorna a dimensão de grid oficial configurada para a respectiva fase da demo.
        /// </summary>
        public static int GetPhaseGridSize(int phase)
        {
            switch (phase)
            {
                case 1: return 7;
                case 2: return 10;
                case 3: return 12;
                default: return 12;
            }
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
        /// Carrega os dados de salvamento da fase indicada. Retorna e salva o design amostrado do Cardinal se não existir ou se as dimensões forem atualizadas.
        /// </summary>
        public static LevelSaveData LoadLevel(int phase)
        {
            int expectedSize = GetPhaseGridSize(phase);
            string path = GetSavePathForPhase(phase);

            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    LevelSaveData data = JsonUtility.FromJson<LevelSaveData>(json);

                    // Sincroniza se o arquivo em disco possuir dimensões legadas
                    if (data != null && data.width == expectedSize && data.depth == expectedSize && data.tiles.Count == expectedSize * expectedSize)
                    {
                        Debug.Log($"[LevelDataManager] Nível da Fase {phase} ({data.width}x{data.depth}) carregado de: {path}");
                        return data;
                    }
                    else
                    {
                        Debug.Log($"[LevelDataManager] Arquivo de save existente da Fase {phase} possui dimensões legadas. Recarregando do Cardinal ({expectedSize}x{expectedSize})...");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[LevelDataManager] Erro ao carregar nível da Fase {phase}: {ex.Message}");
                }
            }

            Debug.Log($"[LevelDataManager] Gerando design fiel da Fase {phase} a partir do JSON de época do Cardinal ({expectedSize}x{expectedSize})...");
            LevelSaveData defaultLevel = GenerateDefaultLevel(phase);
            SaveLevel(phase, defaultLevel);
            return defaultLevel;
        }

        /// <summary>
        /// Prepara os dados de salvamento da próxima fase transferindo os patos sobreviventes promovidos da época anterior.
        /// </summary>
        public static void PrepareNextPhaseWithSurvivors(int nextPhase, List<UnitSaveData> promotedSurvivors)
        {
            LevelSaveData levelData = LoadLevel(nextPhase);
            if (levelData == null)
            {
                levelData = GenerateDefaultLevel(nextPhase);
            }

            int size = GetPhaseGridSize(nextPhase);

            if (promotedSurvivors != null && promotedSurvivors.Count > 0)
            {
                // Remove patos antigos da fase
                levelData.units.RemoveAll(u => u.faction == FactionType.Ducks);

                // Posiciona os patos sobreviventes no flanco inicial do jogador (X=1)
                int startZ = Mathf.Max(1, (size - (promotedSurvivors.Count * 2)) / 2);
                for (int i = 0; i < promotedSurvivors.Count; i++)
                {
                    UnitSaveData duck = promotedSurvivors[i];
                    duck.faction = FactionType.Ducks;
                    duck.x = 1;
                    duck.z = Mathf.Clamp(startZ + i * 2, 1, size - 2);
                    levelData.units.Add(duck);
                }
            }

            // Escala proporcionalmente os gansos inimigos para balancear o desafio da nova época
            int recommendedEnemyLevel = GetEnemyLevelForPhase(nextPhase);
            int recommendedEnemyRank = (int)GetEnemyRankForPhase(nextPhase);
            foreach (var enemy in levelData.units)
            {
                if (enemy.faction == FactionType.Swans)
                {
                    enemy.level = recommendedEnemyLevel;
                    enemy.rank = recommendedEnemyRank;
                    enemy.currentHealth = 0; // Recalcula a nova vida cheia com base nos bônus de nível e patente
                }
            }

            SaveLevel(nextPhase, levelData);
            Debug.Log($"[LevelDataManager] Fase {nextPhase} configurada com {promotedSurvivors?.Count ?? 0} patos promovidos e inimigos escalonados para Nv.{recommendedEnemyLevel} (Rank {(MilitaryRank)recommendedEnemyRank})!");
        }

        /// <summary>
        /// Constrói a estrutura do tabuleiro da fase recortando 100% dos dados dos JSONs de época exportados do Cardinal.
        /// O código define exclusivamente as tropas e posições iniciais para a demo.
        /// </summary>
        private static LevelSaveData GenerateDefaultLevel(int phase)
        {
            int epochYear = GetEpochYearForPhase(phase);
            int size = GetPhaseGridSize(phase);

            // Carrega o snapshot original da simulação física do Cardinal
            LevelSaveData cardinalEpoch = LoadEpoch(epochYear);

            LevelSaveData data = new LevelSaveData
            {
                width = size,
                depth = size,
                heightStep = cardinalEpoch != null && cardinalEpoch.heightStep > 0 ? cardinalEpoch.heightStep : 0.5f,
                tiles = new List<TileSaveData>(),
                units = new List<UnitSaveData>()
            };

            // Recorta com fidelidade matemática a matriz size x size originária do Cardinal
            if (cardinalEpoch != null && cardinalEpoch.tiles != null && cardinalEpoch.tiles.Count > 0)
            {
                foreach (var t in cardinalEpoch.tiles)
                {
                    if (t.x < size && t.z < size)
                    {
                        data.tiles.Add(new TileSaveData
                        {
                            x = t.x,
                            z = t.z,
                            height = t.height,
                            type = t.type
                        });
                    }
                }
            }
            else
            {
                // Fallback seguro caso o snapshot de época não seja localizado
                for (int x = 0; x < size; x++)
                {
                    for (int z = 0; z < size; z++)
                    {
                        data.tiles.Add(new TileSaveData { x = x, z = z, height = 1, type = TerrainType.Field });
                    }
                }
            }

            // Define exclusivamente as tropas e suas posições para o Confronto 1 de cada fase da demo
            if (phase == 1)
            {
                // Fase 1 (Ano 1 – Margens do Rio) | Grid 7x7: 3 Patos Recrutas vs 2 Gansos Recrutas
                data.units.Add(new UnitSaveData { faction = FactionType.Ducks, className = "Warrior", x = 1, z = 1, currentHealth = 100, level = 1, rank = (int)MilitaryRank.Recruit });
                data.units.Add(new UnitSaveData { faction = FactionType.Ducks, className = "Archer", x = 1, z = 3, currentHealth = 100, level = 1, rank = (int)MilitaryRank.Recruit });
                data.units.Add(new UnitSaveData { faction = FactionType.Ducks, className = "Squire", x = 1, z = 5, currentHealth = 100, level = 1, rank = (int)MilitaryRank.Recruit });

                data.units.Add(new UnitSaveData { faction = FactionType.Swans, className = "Warrior", x = 5, z = 2, currentHealth = 30, level = 1, rank = (int)MilitaryRank.Recruit });
                data.units.Add(new UnitSaveData { faction = FactionType.Swans, className = "Squire", x = 5, z = 4, currentHealth = 30, level = 1, rank = (int)MilitaryRank.Recruit });
            }
            else if (phase == 2)
            {
                // Fase 2 (Ano 15 – Colinas de Outono) | Grid 10x10: 3 Patos Veteranos vs 3 Gansos Veteranos
                data.units.Add(new UnitSaveData { faction = FactionType.Ducks, className = "Warrior", x = 1, z = 2, currentHealth = 0, level = 2, rank = (int)MilitaryRank.Veteran });
                data.units.Add(new UnitSaveData { faction = FactionType.Ducks, className = "Archer", x = 1, z = 5, currentHealth = 0, level = 2, rank = (int)MilitaryRank.Veteran });
                data.units.Add(new UnitSaveData { faction = FactionType.Ducks, className = "Squire", x = 1, z = 8, currentHealth = 0, level = 2, rank = (int)MilitaryRank.Veteran });

                int enemyLvl = GetEnemyLevelForPhase(2); // Nv. 3
                int enemyRnk = (int)GetEnemyRankForPhase(2); // Veteran
                data.units.Add(new UnitSaveData { faction = FactionType.Swans, className = "Warrior", x = 8, z = 3, currentHealth = 0, level = enemyLvl, rank = enemyRnk });
                data.units.Add(new UnitSaveData { faction = FactionType.Swans, className = "Archer", x = 8, z = 5, currentHealth = 0, level = enemyLvl, rank = enemyRnk });
                data.units.Add(new UnitSaveData { faction = FactionType.Swans, className = "Squire", x = 8, z = 7, currentHealth = 0, level = enemyLvl, rank = enemyRnk });
            }
            else if (phase == 3)
            {
                // Fase 3 (Ano 30 – Desfiladeiro dos Gansos) | Grid 12x12: 3 Patos Elites vs 3 Gansos Elites
                data.units.Add(new UnitSaveData { faction = FactionType.Ducks, className = "Warrior", x = 1, z = 4, currentHealth = 0, level = 3, rank = (int)MilitaryRank.Elite });
                data.units.Add(new UnitSaveData { faction = FactionType.Ducks, className = "Archer", x = 1, z = 6, currentHealth = 0, level = 3, rank = (int)MilitaryRank.Elite });
                data.units.Add(new UnitSaveData { faction = FactionType.Ducks, className = "Squire", x = 1, z = 8, currentHealth = 0, level = 3, rank = (int)MilitaryRank.Elite });

                int enemyLvl = GetEnemyLevelForPhase(3); // Nv. 5
                int enemyRnk = (int)GetEnemyRankForPhase(3); // Elite
                data.units.Add(new UnitSaveData { faction = FactionType.Swans, className = "Warrior", x = 9, z = 4, currentHealth = 0, level = enemyLvl, rank = enemyRnk });
                data.units.Add(new UnitSaveData { faction = FactionType.Swans, className = "Archer", x = 9, z = 6, currentHealth = 0, level = enemyLvl, rank = enemyRnk });
                data.units.Add(new UnitSaveData { faction = FactionType.Swans, className = "Squire", x = 9, z = 8, currentHealth = 0, level = enemyLvl, rank = enemyRnk });
            }
            else
            {
                // Procedural fallback para fases adicionais (> 3)
                int duckLvl = Mathf.Max(3, phase);
                int duckRnk = (int)(phase >= 4 ? MilitaryRank.Commander : MilitaryRank.Elite);
                data.units.Add(new UnitSaveData { faction = FactionType.Ducks, className = "Warrior", x = 1, z = 2, currentHealth = 0, level = duckLvl, rank = duckRnk });
                data.units.Add(new UnitSaveData { faction = FactionType.Ducks, className = "Archer", x = 1, z = 5, currentHealth = 0, level = duckLvl, rank = duckRnk });
                data.units.Add(new UnitSaveData { faction = FactionType.Ducks, className = "Squire", x = 1, z = 8, currentHealth = 0, level = duckLvl, rank = duckRnk });

                int enemyLvl = GetEnemyLevelForPhase(phase);
                int enemyRnk = (int)GetEnemyRankForPhase(phase);
                data.units.Add(new UnitSaveData { faction = FactionType.Swans, className = "Warrior", x = size - 2, z = 3, currentHealth = 0, level = enemyLvl, rank = enemyRnk });
                data.units.Add(new UnitSaveData { faction = FactionType.Swans, className = "Archer", x = size - 2, z = 5, currentHealth = 0, level = enemyLvl, rank = enemyRnk });
                data.units.Add(new UnitSaveData { faction = FactionType.Swans, className = "Squire", x = size - 2, z = 7, currentHealth = 0, level = enemyLvl, rank = enemyRnk });
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
