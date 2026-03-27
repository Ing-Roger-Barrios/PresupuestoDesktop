using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PresupuestoPro.Services.Pricing;
using PresupuestoPro.ViewModels.Project;
using System.Net;
using System.Reflection;
using PresupuestoPro.Services.Project;

namespace PresupuestoPro.Services.import
{
    internal class DdpInsumo
    {
        public int Id { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Unidad { get; set; } = string.Empty;
        public int IdRelacionadoPrecio { get; set; }
        public decimal Precio { get; set; }
    }

    internal class DdpPartida
    {
        public int Id { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        public string Unidad { get; set; } = string.Empty;
        public int IdModulo { get; set; }
        public int IdRelacionado { get; set; }
        public decimal Rendimiento { get; set; }
    }

    internal class DdpInsumoItem
    {
        public string Tipo { get; set; } = string.Empty;
        public int IdInsumo { get; set; }
        public decimal Coeficiente { get; set; }
    }

    public class DdpParserService
    {
        private const int TAM_IND = 92;
        private const int TAM_DAT = 8;
        private const int TAM_PRE = 460;
        private const int TAM_MOD = 50;

        private const int BLOQUE_MATERIALES = 30;
        private const int BLOQUE_MANO_OBRA = 10;
        private const int BLOQUE_EQUIPO = 20;
        private const int BLOQUE_TOTAL = BLOQUE_MATERIALES + BLOQUE_MANO_OBRA + BLOQUE_EQUIPO;

        private readonly ProjectPricingService? _pricingService;

        public DdpParserService(ProjectPricingService? pricingService = null)
        {
            _pricingService = pricingService;
        }

        public async Task<List<ProjectModuleViewModel>> ParseDdpAsync(
            string ddpFilePath,
            Action<string>? onProgress = null)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            return await Task.Run(() =>
            {
                onProgress?.Invoke("Descomprimiendo archivo .DDP...");

                var tempDir = Path.Combine(
                    Path.GetTempPath(),
                    "PresupuestoPro_" + Guid.NewGuid().ToString("N"));

                try
                {
                    ZipFile.ExtractToDirectory(ddpFilePath, tempDir);
                    sw.Stop(); onProgress?.Invoke($"Descomprimir: {sw.ElapsedMilliseconds}ms");
                    var files = FindProjectFiles(tempDir);

                    onProgress?.Invoke("Leyendo catálogo de insumos (.IND)...");
                    var insumos = ParseInd(files["ind"]);

                    onProgress?.Invoke("Leyendo precios (.DAT)...");
                    // ✅ DAT como array nativo — acceso O(1) por índice
                    var datArray = ParseDatArray(files["dat"]);

                    // Asignar precios a insumos en un solo pass
                    foreach (var ins in insumos.Values)
                    {
                        int idx = ins.IdRelacionadoPrecio - 1;
                        if (idx >= 0 && idx < datArray.Length)
                            ins.Precio = (decimal)datArray[idx].Valor;
                    }

                    onProgress?.Invoke("Leyendo módulos (.MOD)...");
                    var modulos = ParseMod(files["mod"]);

                    onProgress?.Invoke("Leyendo partidas (.PRE)...");
                    var partidas = ParsePre(files["pre"]);

                    onProgress?.Invoke(
                        $"Construyendo {modulos.Count} módulos, {partidas.Count} partidas...");

                     

                    return BuildViewModels(modulos, partidas, insumos, datArray);
                }
                finally
                {
                    try { Directory.Delete(tempDir, recursive: true); } catch { }
                }
            });
        }

        // ── ENCONTRAR ARCHIVOS ────────────────────────────────────────
        private Dictionary<string, string> FindProjectFiles(string basePath)
        {
            var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var allFiles = Directory.GetFiles(basePath, "*.*", SearchOption.AllDirectories);

            foreach (var f in allFiles)
            {
                var ext = Path.GetExtension(f).ToUpperInvariant();
                switch (ext)
                {
                    case ".IND": files["ind"] = f; break;
                    case ".DAT": files["dat"] = f; break;
                    case ".MOD": files["mod"] = f; break;
                    case ".PRE": files["pre"] = f; break;
                    case ".STT": files["stt"] = f; break;
                }
            }

            foreach (var req in new[] { "ind", "dat", "mod", "pre" })
                if (!files.ContainsKey(req))
                    throw new Exception($"Archivo .{req.ToUpper()} no encontrado en el .DDP.");

            return files;
        }

        // ── PARSEAR .IND ──────────────────────────────────────────────
        private Dictionary<int, DdpInsumo> ParseInd(string filePath)
        {
            var result = new Dictionary<int, DdpInsumo>();
            var tipoMap = new Dictionary<byte, string>
            {
                { 0x4D, "Material" },
                { 0x4F, "ManoObra" },
                { 0x45, "Equipo"   }
            };

            var data = File.ReadAllBytes(filePath);
            int total = data.Length / TAM_IND;

            for (int i = 0; i < total; i++)
            {
                int offset = i * TAM_IND;
                var tipoByte = data[offset + 2];
                var tipo = tipoMap.GetValueOrDefault(tipoByte, "Material");
                var idPrecio = BitConverter.ToInt32(data, offset + 88);
                var desc = Latin1ToString(data, offset + 3, 62);
                var unidad = Latin1ToString(data, offset + 75, 5).TrimEnd('\0');

                result[i + 1] = new DdpInsumo
                {
                    Id = i + 1,
                    Tipo = tipo,
                    Descripcion = desc,
                    Unidad = unidad,
                    IdRelacionadoPrecio = idPrecio,
                    Precio = 0
                };
            }

            return result;
        }

        // ── PARSEAR .DAT — array nativo para acceso O(1) ──────────────
        private record DatRegistro(int Id, float Valor);

        private DatRegistro[] ParseDatArray(string filePath)
        {
            var data = File.ReadAllBytes(filePath);
            int count = data.Length / TAM_DAT;
            var arr = new DatRegistro[count];

            for (int i = 0; i < count; i++)
            {
                int offset = i * TAM_DAT;
                var id = BitConverter.ToInt32(data, offset);
                var valor = BitConverter.ToSingle(data, offset + 4);
                if (!float.IsFinite(valor) || Math.Abs(valor) > 1e6f) valor = 0f;
                arr[i] = new DatRegistro(id, valor);
            }

            return arr;
        }

        // ── PARSEAR .MOD ──────────────────────────────────────────────
        private List<(int Id, string Nombre)> ParseMod(string filePath)
        {
            var result = new List<(int, string)>();
            var data = File.ReadAllBytes(filePath);
            int total = data.Length / TAM_MOD;

            // ✅ El ID debe ser la posición real en el archivo (1-based)
            // porque .PRE referencia módulos por posición incluyendo comentarios
            for (int i = 0; i < total; i++)
            {
                int idReal = i + 1;  // posición real, no contar solo los válidos
                var nombre = Latin1ToString(data, i * TAM_MOD, TAM_MOD);

                if (nombre.Length >= 1)
                    result.Add((idReal, nombre));
            }

            return result;
        }

        // ── PARSEAR .PRE ──────────────────────────────────────────────
        private List<DdpPartida> ParsePre(string filePath)
        {
            var result = new List<DdpPartida>();
            var data = File.ReadAllBytes(filePath);
            int total = data.Length / TAM_PRE;

            for (int i = 0; i < total; i++)
            {
                int offset = i * TAM_PRE;
                var desc = Latin1ToString(data, offset + 4, 63);
                if (string.IsNullOrWhiteSpace(desc)) continue;

                var unidad = Windows1252ToString(data, offset + 76, 5).TrimEnd('\0');
                var idModulo = data[offset + 2];
                var idRelacionado = BitConverter.ToInt32(data, offset + 81);

                decimal rendimiento = 0;
                if (offset + 93 <= data.Length)
                {
                    var d = BitConverter.ToDouble(data, offset + 85);
                    if (double.IsFinite(d) && Math.Abs(d) < 1e6)
                        rendimiento = (decimal)d;
                }

                result.Add(new DdpPartida
                {
                    Id = result.Count + 1,
                    Descripcion = desc,
                    Unidad = unidad,
                    IdModulo = idModulo,
                    IdRelacionado = idRelacionado,
                    Rendimiento = rendimiento
                });
            }

            return result;
        }

        // ── OBTENER INSUMOS — acceso directo O(1) al array ───────────
        private List<DdpInsumoItem> GetInsumosItem(
            DatRegistro[] dat,
            int idRelacionado,
            Dictionary<int, DdpInsumo> insumos)
        {
            var result = new List<DdpInsumoItem>(BLOQUE_TOTAL);
            int base0 = idRelacionado - 1;

            // Offsets de los tres bloques
            var bloques = new (int inicio, int tam, string tipo)[]
            {
                (base0,                                    BLOQUE_MATERIALES, "Material"),
                (base0 + BLOQUE_MATERIALES,                BLOQUE_MANO_OBRA,  "ManoObra"),
                (base0 + BLOQUE_MATERIALES + BLOQUE_MANO_OBRA, BLOQUE_EQUIPO, "Equipo"),
            };

            foreach (var (inicio, tam, tipoEsperado) in bloques)
            {
                int fin = Math.Min(inicio + tam, dat.Length);
                for (int i = inicio; i < fin; i++)
                {
                    var reg = dat[i];
                    if (reg.Id == 0) continue;

                    // ✅ Lookup O(1) al diccionario de insumos
                    if (insumos.TryGetValue(reg.Id, out var ins) &&
                        ins.Tipo == tipoEsperado)
                    {
                        result.Add(new DdpInsumoItem
                        {
                            Tipo = ins.Tipo,
                            IdInsumo = reg.Id,
                            Coeficiente = (decimal)reg.Valor
                        });
                    }
                }
            }

            return result;
        }

        // ── CONSTRUIR VIEWMODELS ──────────────────────────────────────
        private List<ProjectModuleViewModel> BuildViewModels(
            List<(int Id, string Nombre)> modulos,
            List<DdpPartida> partidas,
            Dictionary<int, DdpInsumo> insumos,
            DatRegistro[] dat)
        {
            var result = new List<ProjectModuleViewModel>(modulos.Count);
            var modulosMap = new Dictionary<int, ProjectModuleViewModel>(modulos.Count);

            // Suspender eventos durante carga masiva
            GlobalResourceService.IsSuspended = true;
            GlobalItemService.IsSuspended = true;
            try
            {

                foreach (var (id, nombre) in modulos)
                {
                    var vm = new ProjectModuleViewModel { Name = nombre };
                    result.Add(vm);
                    modulosMap[id] = vm;
                }

                foreach (var partida in partidas)
                {
                    if (!modulosMap.TryGetValue(partida.IdModulo, out var moduleVm))
                        continue;

                    var itemVm = new ProjectItemViewModel(_pricingService)
                    {
                        Code = $"ITEM_{partida.Id:D4}",
                        Description = partida.Descripcion,
                        Unit = partida.Unidad,
                        Quantity = partida.Rendimiento
                    };

                    var insumosItem = GetInsumosItem(dat, partida.IdRelacionado, insumos);

                    // Ordenar recursos por tipo antes de agregarlos
                    var recursosOrdenados = insumosItem
                        .OrderBy(r => r.Tipo switch {
                            "Material" => 1,
                            "ManoObra" => 2,
                            "Equipo" => 3,
                            _ => 4
                        });

                    foreach (var insumoItem in recursosOrdenados)
                    {
                        if (!insumos.TryGetValue(insumoItem.IdInsumo, out var ins)) continue;

                        var resourceVm = new ProjectResourceViewModel(
                            () => itemVm.RecalculateUnitPrice())
                        {
                            ResourceType = ins.Tipo,
                            ResourceName = ins.Descripcion,
                            Unit = ins.Unidad,
                            Performance = insumoItem.Coeficiente,
                            UnitPrice = ins.Precio
                        };

                        resourceVm.InitializeWithGlobalPrice();
                        itemVm.Resources.Add(resourceVm);
                    }

                    itemVm.InitializeWithGlobalConfiguration();
                    itemVm.RecalculateUnitPrice();
                    moduleVm.Items.Add(itemVm);
                }

            } // end try
            finally { GlobalResourceService.IsSuspended = false; GlobalItemService.IsSuspended = false; }

            return result.Where(m => m.Items.Count > 0).ToList();
        }

        // ── HELPERS DE ENCODING ───────────────────────────────────────
        private static readonly Encoding _latin1 = Encoding.Latin1;

        private static string Latin1ToString(byte[] data, int offset, int length)
        {
            int len = Math.Min(length, data.Length - offset);
            if (len <= 0) return string.Empty;
            var s = _latin1.GetString(data, offset, len);
            // Quitar caracteres de control y null terminators
            int end = s.IndexOf('\0');
            if (end >= 0) s = s[..end];
            return s.Trim();
        }

        private static string Windows1252ToString(byte[] data, int offset, int length)
        {
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                var enc = Encoding.GetEncoding(1252);
                int len = Math.Min(length, data.Length - offset);
                if (len <= 0) return string.Empty;
                var s = enc.GetString(data, offset, len);
                int end = s.IndexOf('\0');
                if (end >= 0) s = s[..end];
                return s.Trim();
            }
            catch { return string.Empty; }
        }
    }
}
