using System;
using System.Collections.Generic;
using System.Linq;
using Voxif.AutoSplitter;
using Voxif.Helpers.Unity;
using Voxif.Helpers.MemoryHelper;
using Voxif.IO;
using Voxif.Memory;
using LiveSplit.ComponentUtil;

namespace LiveSplit.DodgeballAcademia {
    public class DodgeballAcademiaMemory : Memory {

        protected override string[] ProcessNames => new string[] { "DodgeballAcademia" };
        
        public Pointer<IntPtr> Trackers { get; private set; }

        public Pointer<bool> ShowTitleScreen { get; private set; }
        public StringPointer TransitionText { get; private set; }
        public Pointer<bool> TitleCanNavigate { get; private set; }
        public Pointer<int> TitleItemSelected { get; private set; }
        public Pointer<bool> TitleClicking { get; private set; }

        private UnityHelperTask unityTask;
        private ScanHelperTask scanTask;

        private byte[] loadBytes, updateBytes;
        private IntPtr allocAddr, loadAddr, updateAddr;

        private readonly Dictionary<string, int> trackers = new Dictionary<string, int>();

        public DodgeballAcademiaMemory(Logger logger) : base(logger) {
            OnHook += () => {
                scanTask = new ScanHelperTask(game, logger);
                scanTask.Run(new ScannableData() {
                    { "GameAssembly.dll",
                        new Dictionary<string, ScanTarget>() {
                            { "load", new ScanTarget(0x6, "48 8B C8 33 D2 E8 ???????? 48 8B 5C 24 ?? 48 8B 6C 24") },
                            { "update", new ScanTarget(0x3, "33 C9 E8 ???????? 48 8B 5B ?? 33 C9 E8") },
                        }
                    }
                }, InitScan);

                unityTask = new UnityHelperTask(game, logger);
                unityTask.Run(InitPointers);
            };

            OnExit += () => {
                if(scanTask != null) {
                    scanTask.Dispose();
                    scanTask = null;
                }

                if(allocAddr != default) {
                    game.Process.Suspend();
                    game.Write(loadBytes, loadAddr);
                    game.Write(updateBytes, updateAddr);
                    game.Process.FreeMemory(allocAddr);
                    game.Process.Resume();
                }

                if(unityTask != null) {
                    unityTask.Dispose();
                    unityTask = null;
                }
            };
        }

        //TODO replace with a read-only system
        private void InitScan(ScannableResult result) {
            var resAsm = result["GameAssembly.dll"];

            loadAddr = game.FromAssemblyAddress(resAsm["load"]) + 0x2;
            updateAddr = game.FromAssemblyAddress(resAsm["update"]);

            loadBytes = game.Read(loadAddr, 14);
            updateBytes = game.Read(updateAddr, 13);

            game.Process.Suspend();

            allocAddr = game.Process.AllocateMemory(128);

            game.Write(     new byte[] { 0xC7, 0x05, 0x20, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00 }                 // mov [rip+20], 1
                    .Concat(new byte[] { 0x48, 0x83, 0xEC, 0x20 })                                                    // sub rsp,20
                    .Concat(new byte[] { 0x48, 0xBB }).Concat((game.FromAssemblyAddress(loadAddr + 6) + 1).ToBytes()) // mov rbx,GameAssembly.dll + XXXXXXX
                    .Concat(new byte[] { 0x80, 0x3B, 0x00 })                                                          // cmp byte ptr[rbx],00
                    .Concat(new byte[] { 0x48, 0x8B, 0xD9 })                                                          // mov rbx,rcx                                                                          // (add copy)
                    .Concat(new byte[] { 0x48, 0xB8 }).Concat((loadAddr + 14).ToBytes())                              // mov rax,retAddr
                    .Concat(new byte[] { 0xFF, 0xE0 })                                                                // jmp rax
                    .Concat(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 })                            // (memory)
                    .ToArray(),
                allocAddr
            );

            game.Write(
                new byte[] { 0x48, 0xB8 }.Concat(allocAddr.ToBytes()).Concat(new byte[] { 0xFF, 0xE0, 0x66, 0x90 }).ToArray(),
                loadAddr
            );

            game.Write(     new byte[] { 0xC7, 0x05, 0xEE, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00 }                   // mov [rip-12], 0
                    .Concat(new byte[] { 0x48, 0x83, 0xEC, 0x28 })                                                      // sub rsp,28
                    .Concat(new byte[] { 0x48, 0xB9 }).Concat((game.FromAssemblyAddress(updateAddr + 6) + 1).ToBytes()) // mov rcx,GameAssembly.dll + XXXXXXX
                    .Concat(new byte[] { 0x80, 0x39, 0x00 })                                                            // cmp byte ptr[rcx],00
                    .Concat(new byte[] { 0x75, 0x0C })                                                                  // jne rip+0C
                    .Concat(new byte[] { 0x48, 0xB9 }).Concat((updateAddr + 13).ToBytes())                              // mov rcx,retAddr
                    .Concat(new byte[] { 0xFF, 0xE1 })                                                                  // jmp rcx
                    .Concat(new byte[] { 0x48, 0xB9 }).Concat((updateAddr + 13 + 0x12).ToBytes())                       // mov rcx,retAddr+12
                    .Concat(new byte[] { 0xFF, 0xE1 })                                                                  // jmp rcx
                    .ToArray(),
                allocAddr + 0x32
            );

            game.Write(
                new byte[] { 0x48, 0xB8 }.Concat((allocAddr + 0x32).ToBytes()).Concat(new byte[] { 0xFF, 0xE0, 0x90 }).ToArray(),
                updateAddr
            );

            game.Process.Resume();

            scanTask = null;
        }

        private void InitPointers(IMonoHelper unity) {
            MonoNestedPointerFactory ptrFactory = new MonoNestedPointerFactory(game, unity);

            Trackers = ptrFactory.Make<IntPtr>("GameStats", "current", unity.GetFieldOffset("FullStats", "trackers"));

            var overworldDirectorStatic = ptrFactory.Make("Overworld.Director", out IntPtr directorClass);

            ShowTitleScreen = ptrFactory.Make<bool>(overworldDirectorStatic, "showTitleScreen");

            var overworldDirector = ptrFactory.Make<IntPtr>(overworldDirectorStatic, "persistentOverworldDirector");

            TransitionText = ptrFactory.MakeString(overworldDirector, unity.GetFieldOffset(directorClass, "transitionFade"), 0x38, 0x20, 0x10, ptrFactory.StringHeaderSize);
            TransitionText.StringType = EStringType.UTF16Sized;
            
            var titleScreenList = ptrFactory.Make<IntPtr>(overworldDirector, unity.GetFieldOffset(directorClass, "titleScreen"), 0x28);
            var listClass = unity.FindClass("ListManager");
            TitleCanNavigate = ptrFactory.Make<bool>(titleScreenList, unity.GetFieldOffset(listClass, "canNavigate"));
            TitleItemSelected = ptrFactory.Make<int>(titleScreenList, unity.GetFieldOffset(listClass, "itemSelected"));
            TitleClicking = ptrFactory.Make<bool>(titleScreenList, unity.GetFieldOffset(listClass, "clicking"));

            logger.Log(ptrFactory.ToString());

            unityTask = null;
        }

        public override bool Update() {
            return base.Update() && scanTask == null && unityTask == null;
        }

        public void ResetData() {
            trackers.Clear();
        }

        public IEnumerable<string> NewTrackerSequence(bool useSavedData = true) {
            if(ShowTitleScreen.New) {
                yield break;
            }
            int count = game.Read<int>(Trackers.New + 0x20);
            IntPtr entries = game.Read<IntPtr>(Trackers.New + 0x18);
            for(int id = 0; id < count; id++) {
                IntPtr entry = entries + 0x28 + 0x18 * id;
                string key = game.ReadString(game.Read(entry, 0x0, 0x14), EStringType.UTF16Sized);
                int value = game.Read<int>(game.Read(entry, 0x8, 0x10));
                if(useSavedData) {
                    if(trackers.ContainsKey(key)) {
                        if(trackers[key] != value) {
                            trackers[key] = value;
                            yield return key + "_" + value;
                        }
                    } else {
                        trackers.Add(key, value);
                        yield return key + "_" + value;
                    }
                } else {
                    yield return key + "_" + value;
                }
            }
        }

        public string GetEpisode() {
            foreach(string name in NewTrackerSequence(false)) {
                if(name.StartsWith("episode_")) {
                    return name;
                }
            }
            return String.Empty;
        }

        public bool IsLoading() {
            return allocAddr != default && game.Read<bool>(allocAddr + 0x2A);
        }
    }
}