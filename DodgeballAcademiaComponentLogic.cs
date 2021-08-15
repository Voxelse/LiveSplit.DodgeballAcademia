using System;
using System.Linq;

namespace LiveSplit.DodgeballAcademia {
    public partial class DodgeballAcademiaComponent {

        private readonly RemainingDictionary remainingSplits;

        public override bool Update() {
            return memory.Update();
        }

        public override bool Start() {
            return memory.TitleCanNavigate.New && memory.TitleClicking.New && !memory.TitleClicking.Old && memory.TitleItemSelected.New == 0;
        }

        public override void OnStart() {
            remainingSplits.Setup(settings.Splits);
            memory.ResetData();
        }

        public override bool Split() {
            const string T = "Tracker", TT = "TransitionText";

            return remainingSplits.Count() != 0 && (SplitTransitionText() || SplitTracker());

            bool SplitTransitionText() {
                if(!remainingSplits.ContainsKey(TT)
                || !memory.TransitionText.Changed || String.IsNullOrEmpty(memory.TransitionText.New)) {
                    return false;
                }
                switch(memory.TransitionText.New) {
                    case "ui_thenextday":
                    case "ui_theend":
                        return remainingSplits.Split(TT, memory.GetEpisode());
                    default:
                        return false;
                }
            }
            
            bool SplitTracker() {
                if(!remainingSplits.ContainsKey(T)) {
                    return false;
                }
                foreach(string name in memory.NewTrackerSequence()) {
                    if(remainingSplits.Split(T, name)) {
                        return true;
                    }
                }
                return false;
            }
        }

        public override bool Reset() {
            return memory.TitleCanNavigate.New && !memory.TitleCanNavigate.Old;
        }

        public override bool Loading() {
            return memory.IsLoading();
        }
    }
}