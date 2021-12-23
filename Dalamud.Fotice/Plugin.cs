using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Dalamud.Plugin;
using Newtonsoft.Json;

namespace Fotice
{
    public class Plugin : IDalamudPlugin, IDisposable
    {
        public string Name => "Dalamud.Fotice";

        public Plugin(DalamudPluginInterface pluginInterface)
        {
            Task.Factory.StartNew(Patch);
        }

        public void Dispose()
        {
        }

        public static async void Patch()
        {
            using var client = new HttpClient();

            using var req = new HttpRequestMessage(
                HttpMethod.Get,
                "https://raw.githubusercontent.com/RyuaNerin/Fotice/master/patch.json"
            );
            req.Headers.Add("User-Agent", "Dalamud.Fotice");

            using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseContentRead);
            
            var content = await resp.Content.ReadAsStringAsync();

            var patches = JsonConvert.DeserializeObject<PatchData>(content);

            using var process = Process.GetCurrentProcess();

            var moduleAddr = process.MainModule.BaseAddress;
            var moduleSize = process.MainModule.ModuleMemorySize;

            foreach (var patch in patches.Patches)
            {
                MemoryPatch(moduleAddr, moduleSize, patch);
            }
        }

        private static void MemoryPatch(IntPtr modBaseAddr, int modBaseSize, PatchData.Patch patch)
        {
            var bef = ParseHex(patch.Bef);
            var aft = ParseHex(patch.Aft);

            var modBaseLimit = modBaseAddr + modBaseSize - bef.Length;

            var buff = new byte[64 * 1024];

            while (modBaseAddr.ToInt64() < modBaseLimit.ToInt64())
            {
                var toRead = Math.Min(buff.Length, (modBaseLimit - modBaseSize).ToInt32());
                Marshal.Copy(modBaseAddr, buff, 0, toRead);

                var offset = FindArray(buff, bef, 0, toRead);
                if (offset != -1)
                {
                    for (int i = 0; i < aft.Length; i++)
                    {
                        buff[i] = aft[i] != -1 ? (byte)aft[i] : buff[offset + i];
                    }

                    Marshal.Copy(buff, 0, modBaseAddr + offset, aft.Length);

                    modBaseAddr += offset + aft.Length;
                }
                else
                {
                    modBaseAddr += toRead - aft.Length + 1;
                }
            }
        }

        private static int FindArray(byte[] buff, short[] pattern, int startIndex, int len)
        {
            len = Math.Min(buff.Length, len);

            int i, j;
            for (i = startIndex; i < (len - pattern.Length); i++)
            {
                for (j = 0; j < pattern.Length; j++)
                    if (pattern[j] != -1 && buff[i + j] != pattern[j])
                        break;

                if (j == pattern.Length)
                    return i;
            }

            return -1;
        }

        private static short[] ParseHex(string str)
        {
            var lst = new List<short>();

            var i = 0;
            while (i < str.Length)
            {
                if (str[i] == ' ')
                {
                    i++;
                    continue;
                }

                if (str[i] == '?')
                {
                    i++;
                    lst.Add(-1);
                    continue;
                }

                lst.Add(short.Parse(str.Substring(i, 2), NumberStyles.HexNumber));
                i += 2;
            }

            return lst.ToArray();
        }

        public class PatchData
        {
            [JsonProperty("patch")]
            public Patch[] Patches { get; set; }

            public class Patch
            {
                [JsonProperty("bef")] public string Bef { get; set; }
                [JsonProperty("aft")] public string Aft { get; set; }
                //[JsonProperty("req")] public bool   Req { get; set; }
            }
        }
    }
}
