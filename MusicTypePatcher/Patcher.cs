using Mutagen.Bethesda;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Records;

namespace MusicTypePatcher
{
    public class Patcher
    {
        private static uint Timestamp { get; } = (uint)(Math.Max(1, DateTime.Today.Year - 2000) << 9 | DateTime.Today.Month << 5 | DateTime.Today.Day);

        public static async Task<int> Main(string[] args)
        {
            return await SynthesisPipeline.Instance
                .SetTypicalOpen(GameRelease.SkyrimSE, "MusicTypes Merged.esp")
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(Apply)
                .Run(args);
        }

        private static IEnumerable<IModContext<TGet>> ExtentContexts<TGet>(
            IPatcherState<ISkyrimMod, ISkyrimModGetter> state,
            IReadOnlyDictionary<ModKey, ModKey[]> masterLookup,
            IFormLinkGetter<TGet> link)
            where TGet : class, IMajorRecordGetter
        {
            // Get every context for this musicType
            var contexts = link.ResolveAllSimpleContexts(state.LinkCache).ToArray();
            var masters = contexts.SelectMany(i => masterLookup.GetValueOrDefault(i.ModKey) ?? []).ToHashSet();

            foreach (var ctx in contexts)
            {
                if (!masters.Contains(ctx.ModKey))
                    yield return ctx;
            }
        }

        private static void Apply(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            using var loadOrder = state.LoadOrder;

            var mastersMapping = loadOrder.ListedOrder
                .Where(x => x.Mod != null)
                .ToDictionary(
                    x => x.ModKey,
                    x => x.Mod!.MasterReferences.Select(x => x.Master).ToArray());

            foreach (var musicType in loadOrder.PriorityOrder.OnlyEnabled().MusicType().WinningOverrides())
            {
                Console.WriteLine("Processing MusicType {0}", musicType);
                ProcessMusicType(state, mastersMapping, musicType.ToLink());
            }
        }

        private static void ProcessMusicType(
            IPatcherState<ISkyrimMod, ISkyrimModGetter> state,
            IReadOnlyDictionary<ModKey, ModKey[]> masterLookup,
            IFormLinkGetter<IMusicTypeGetter> musicType)
        {
            var origin = musicType.Resolve(state.LinkCache);
            var extentContexts = ExtentContexts(state, masterLookup, musicType).ToList();
            if (extentContexts.Count < 2)
            {
                return;
            }

            var copy = state.PatchMod.MusicTypes.GetOrAddAsOverride(origin);
            copy.FormVersion = 44;
            copy.VersionControl = Timestamp;
            copy.Tracks = new ExtendedList<IFormLinkGetter<IMusicTrackGetter>>();

            var originalTracks = origin.Tracks.EmptyIfNull().ToArray();
            int originalTrackCount = originalTracks.Length;

            var extentTracks = extentContexts.Select(static i => i.Record.Tracks.EmptyIfNull().ToArray()).ToArray();

            extentTracks.Aggregate(originalTracks, (i, k) => i.Intersection(k).ToArray()).ForEach(copy.Tracks.Add);
            copy.Tracks.AddRange(extentTracks.SelectMany(i => i.DisjunctLeft(copy.Tracks)));

            Console.WriteLine("Copied {0} tracks to {1}", copy.Tracks.Count - originalTrackCount, copy.EditorID);
        }
    }
}
