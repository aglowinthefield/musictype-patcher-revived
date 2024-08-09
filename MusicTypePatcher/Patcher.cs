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

        private static IEnumerable<IModContext<TGet>> ExtentContexts<TGet>(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, FormKey formKey)
            where TGet : class, IMajorRecordGetter
        {
            // Get every context for this musicType
            var contexts = state.LinkCache.ResolveAllSimpleContexts<TGet>(formKey).ToArray();
            var masters = contexts.SelectMany(i => state.LoadOrder.TryGetValue(i.ModKey)?.Mod?.MasterReferences ?? new List<IMasterReferenceGetter>(), (i, g) => g.Master)
                .ToHashSet();

            foreach (var ctx in contexts)
            {
                if (!masters.Contains(ctx.ModKey))
                    yield return ctx;
            }
        }

        private static void Apply(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            using var loadOrder = state.LoadOrder;

            foreach (var musicType in loadOrder.PriorityOrder.OnlyEnabled().MusicType().WinningOverrides())
            {
                Console.WriteLine("Processing MusicType {0}", musicType);
                ProcessMusicType(state, musicType);
            }
        }

        private static void ProcessMusicType(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, IMusicTypeGetter musicType)
        {
            var origin = state.LinkCache.Resolve<IMusicTypeGetter>(musicType.FormKey);
            var extentContexts = ExtentContexts<IMusicTypeGetter>(state, musicType.FormKey).ToList();
            if (extentContexts.Count < 2)
            {
                return;
            }

            var copy = state.PatchMod.MusicTypes.GetOrAddAsOverride(origin);
            copy.FormVersion = 44;
            copy.VersionControl = Timestamp;
            copy.Tracks = new ExtendedList<IFormLinkGetter<IMusicTrackGetter>>();

            var originalTracks = origin.Tracks.EmptyIfNull();
            int originalTrackCount = originalTracks.Count();

            var extentTracks = extentContexts.Select(static i => i.Record.Tracks.EmptyIfNull());

            extentTracks.Aggregate(originalTracks, (i, k) => i.Intersection(k)).ForEach(copy.Tracks.Add);
            copy.Tracks.AddRange(extentTracks.SelectMany(i => i.DisjunctLeft(copy.Tracks)));

            Console.WriteLine("Copied {0} tracks to {1}", copy.Tracks.Count - originalTrackCount, copy.EditorID);
        }
    }
}
