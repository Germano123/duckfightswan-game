using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Cardinal.TerrainEngine.Native
{
    /// <summary>
    /// P/Invoke Bridge que conecta diretamente à biblioteca nativa C-API do Cardinal Core (cardinal.dll / .so / .dylib).
    /// </summary>
    public static class CardinalNativeBridge
    {
        private const string LibName = "cardinal";

        private static bool? _isAvailable;

        /// <summary>
        /// Verifica se a biblioteca nativa está presente no sistema ou pasta de Plugins.
        /// </summary>
        public static bool IsNativeLibraryAvailable()
        {
            if (_isAvailable.HasValue) return _isAvailable.Value;

            try
            {
                // Teste de chamada sem efeito colateral
                uint count = get_biome_type_count();
                _isAvailable = (count == 7);
            }
            catch (DllNotFoundException)
            {
                _isAvailable = false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CardinalNativeBridge] Erro ao testar biblioteca nativa: {ex.Message}");
                _isAvailable = false;
            }

            return _isAvailable.Value;
        }

        [DllImport(LibName, EntryPoint = "create_simulation")]
        public static extern IntPtr create_simulation(uint gridSize, uint hrSize, uint seed);

        [DllImport(LibName, EntryPoint = "destroy_simulation")]
        public static extern void destroy_simulation(IntPtr ctx);

        [DllImport(LibName, EntryPoint = "generate_planet")]
        public static extern void generate_planet(
            IntPtr ctx, double latMin, double latMax, double lonMin, double lonMax, 
            int octaves, double scale, double seaLevel, double oceanDepth);

        [DllImport(LibName, EntryPoint = "set_global_temperatures")]
        public static extern void set_global_temperatures(IntPtr ctx, double maxEqTemp, double minPoleTemp);

        [DllImport(LibName, EntryPoint = "get_latitude_temperature")]
        public static extern double get_latitude_temperature(IntPtr ctx, uint macroY);

        [DllImport(LibName, EntryPoint = "create_local_patch")]
        public static extern IntPtr create_local_patch(IntPtr ctx, uint cellX, uint cellY);

        [DllImport(LibName, EntryPoint = "destroy_local_patch")]
        public static extern void destroy_local_patch(IntPtr patch);

        [DllImport(LibName, EntryPoint = "get_patch_window_resolution")]
        public static extern uint get_patch_window_resolution(IntPtr patch);

        [DllImport(LibName, EntryPoint = "tick_local_patch")]
        public static extern void tick_local_patch(IntPtr patch, IntPtr ctx, double meanTemp, double dt, double evapMult);

        [DllImport(LibName, EntryPoint = "get_patch_elevation_data")]
        public static extern IntPtr get_patch_elevation_data(IntPtr patch);

        [DllImport(LibName, EntryPoint = "get_patch_water_data")]
        public static extern IntPtr get_patch_water_data(IntPtr patch);

        [DllImport(LibName, EntryPoint = "get_patch_soil_data")]
        public static extern IntPtr get_patch_soil_data(IntPtr patch);

        [DllImport(LibName, EntryPoint = "get_patch_total_biomass_data")]
        public static extern IntPtr get_patch_total_biomass_data(IntPtr patch);

        [DllImport(LibName, EntryPoint = "get_patch_dominant_biome_data")]
        public static extern IntPtr get_patch_dominant_biome_data(IntPtr patch);

        [DllImport(LibName, EntryPoint = "get_patch_dominance_share_data")]
        public static extern IntPtr get_patch_dominance_share_data(IntPtr patch);

        [DllImport(LibName, EntryPoint = "get_biome_type_count")]
        public static extern uint get_biome_type_count();
    }
}
