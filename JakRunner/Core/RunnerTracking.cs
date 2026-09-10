using System;
using System.Activities.Tracking;
using Custom.Shared;

namespace JakRunner.Core
{
    /// <summary>
    /// Pencatat jalannya workflow, activity demi activity.
    ///
    /// Inilah yang membuat panel log JakRunner dan dasbor ForgeHub menunjukkan
    /// apa yang SEDANG dikerjakan robot, bukan sekadar "mulai" lalu "selesai"
    /// dengan kekosongan di antaranya. Tanpa ini, satu jalan yang memakan lima
    /// menit tidak memberi tanda apa pun bahwa ia masih hidup.
    ///
    /// Yang dicatat hanya activity yang PUNYA NAMA yang berarti — activity
    /// pembungkus yang dibuat runtime dilewati. Mencatat semuanya menghasilkan
    /// ratusan baris per detik yang justru menyembunyikan yang penting.
    /// </summary>
    public class RunnerTracking : TrackingParticipant
    {
        public RunnerTracking()
        {
            // Profil menyaring di SUMBERNYA. Menyaring belakangan di Track()
            // tetap membuat runtime menyusun record untuk setiap kejadian —
            // pekerjaan yang seluruhnya terbuang.
            TrackingProfile = new TrackingProfile
            {
                Name = "JakRunner",
                Queries =
                {
                    new ActivityStateQuery
                    {
                        ActivityName = "*",
                        States = { ActivityStates.Executing, ActivityStates.Faulted },
                    },
                    new CustomTrackingQuery { Name = "*", ActivityName = "*" },
                    new WorkflowInstanceQuery
                    {
                        States = { WorkflowInstanceStates.Started, WorkflowInstanceStates.Completed },
                    },
                },
            };
        }

        protected override void Track(TrackingRecord record, TimeSpan timeout)
        {
            var activity = record as ActivityStateRecord;

            if (activity != null)
            {
                var name = activity.Activity != null ? activity.Activity.Name : null;
                if (!Interesting(name)) return;

                if (activity.State == ActivityStates.Faulted)
                    RobotLog.Error("Gagal di " + name, name);
                else
                    RobotLog.Debug(name, name);

                return;
            }

            var instance = record as WorkflowInstanceRecord;

            if (instance != null)
            {
                if (instance.State == WorkflowInstanceStates.Started) RobotLog.Info("Workflow dimulai.");
                else if (instance.State == WorkflowInstanceStates.Completed) RobotLog.Info("Workflow selesai.");
            }
        }

        /// <summary>
        /// Activity yang layak muncul di log.
        ///
        /// Runtime WF menyisipkan banyak activity pembantu yang tidak pernah
        /// ditulis siapa pun di kanvas — ekspresi VisualBasicValue, pembungkus
        /// argumen, dan sejenisnya. Nama-nama itu tidak berarti apa pun bagi
        /// orang yang membaca log.
        /// </summary>
        private static bool Interesting(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;

            if (name.StartsWith("VisualBasicValue", StringComparison.Ordinal)) return false;
            if (name.StartsWith("VisualBasicReference", StringComparison.Ordinal)) return false;
            if (name.StartsWith("Literal<", StringComparison.Ordinal)) return false;
            if (name.StartsWith("VariableValue", StringComparison.Ordinal)) return false;
            if (name.StartsWith("VariableReference", StringComparison.Ordinal)) return false;
            if (name.StartsWith("ArgumentValue", StringComparison.Ordinal)) return false;
            if (name.StartsWith("LocationHelper", StringComparison.Ordinal)) return false;
            if (name.Contains("`")) return false;

            return true;
        }
    }
}
