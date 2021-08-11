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
            return remainingSplits.Count() != 0 && (SplitTracker());

            bool SplitTracker() {
                if(!remainingSplits.ContainsKey("Tracker")) {
                    return false;
                }
                foreach(string name in memory.NewTrackerSequence()) {
                    if(remainingSplits.Split("Tracker", name)) {
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