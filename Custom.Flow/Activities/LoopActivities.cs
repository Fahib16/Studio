using System;
using System.Activities;
using System.Activities.Presentation;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Markup;

namespace Custom.Flow.Activities
{
    /// <summary>
    /// Ulangi untuk setiap baris sebuah DataTable.
    ///
    /// Pengganti OpenRPA.Activities.ForEachDataRow, yang hidup di dalam rakitan
    /// Studio — sehingga workflow yang memakainya gagal dimuat JakRunner dengan
    /// "Cannot create unknown type 'ForEachDataRow'", padahal jalan normal di
    /// kanvas Studio.
    /// </summary>
    [Designer(typeof(ForEachDataRowDesigner))]
    [ContentProperty("Body")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.foreachdatarow.png")]
    [DisplayName("For Each Data Row")]
    [Description("Ulangi isi badan untuk setiap baris DataTable atau DataView.")]
    public class ForEachDataRow : JakForgeLoop, IActivityTemplateFactory
    {
        [Category("Input")]
        [Description("Tabel yang barisnya diulang. Kosongkan kalau memakai Data view.")]
        public InArgument<DataTable> DataTable { get; set; }

        [Category("Input")]
        [Description("Dipakai kalau Data table dikosongkan; berguna untuk baris yang sudah disaring atau diurutkan.")]
        public InArgument<DataView> DataView { get; set; }

        [Browsable(false)]
        public ActivityAction<DataRowView> Body { get; set; }

        // Daftar baris DAN posisi berikutnya -- BUKAN enumerator.
        //
        // List<T>.GetEnumerator() mengembalikan STRUCT. Menyimpannya lewat
        // context.SetValue ke Variable<IEnumerator<...>> mengotakkan
        // SALINANNYA, sehingga MoveNext() pada variabel lokal dan MoveNext()
        // pada nilai yang tersimpan menggerakkan DUA posisi yang berbeda.
        //
        // Akibatnya baris pertama dikerjakan DUA KALI, seluruh baris sesudahnya
        // bergeser satu, dan perulangannya berjalan sekali lebih banyak
        // daripada jumlah barisnya. Kompilernya tidak mengeluh sedikit pun.
        //
        // Di RPA Challenge gejalanya persis "7 dari 70 isian benar": hanya
        // putaran pertama yang cocok, sisanya meleset satu baris. Studio tidak
        // kena karena Studio memakai ForEachDataRow milik OpenRPA, bukan yang
        // ini -- itulah sebabnya workflow yang sama memberi 100% di Studio dan
        // 10% di JakRunner.
        //
        // Indeks eksplisit tidak punya jebakan itu, dan langsung sejalan dengan
        // Index/Total yang memang sudah dipakai.
        private readonly Variable<List<DataRowView>> _rows =
            new Variable<List<DataRowView>>("_rows");

        private readonly Variable<int> _next = new Variable<int>("_next");

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            // Refleksi bawaan NativeActivity yang mendaftarkan DataTable,
            // DataView, delegate Body, dan koleksi Variables.
            //
            // DataTable dan DataView sengaja TIDAK ditandai RequiredArgument:
            // yang wajib adalah salah SATUNYA terisi, dan itu diperiksa saat
            // berjalan. Menandai keduanya wajib akan menolak workflow yang sah.
            base.CacheMetadata(metadata);

            // Variabel implementasi ditambahkan SESUDAH refleksi.
            //
            // Menambahkannya lebih dulu membuat pass refleksi berjalan di atas
            // metadata yang sudah berisi, dan argumen publiknya terdaftar dua
            // kali — WF lalu menolak seluruh workflow dengan "Argument ... bound
            // to more than one RuntimeArgument object".
            metadata.AddImplementationVariable(_rows);
            metadata.AddImplementationVariable(_next);
        }

        protected override void StartLoop(NativeActivityContext context)
        {
            var table = DataTable.Get(context);
            var view = table != null ? table.DefaultView : DataView.Get(context);

            if (view == null)
                throw new InvalidOperationException(
                    "For Each Data Row: Data table dan Data view dua-duanya kosong.");

            // Barisnya disalin ke daftar lebih dulu.
            //
            // DataView adalah pandangan HIDUP atas tabelnya: kalau badan
            // perulangan menambah atau menghapus baris, pembacaan langsung akan
            // melempar di tengah jalan. Menyalin lebih dulu membuat perulangan
            // mengerjakan persis baris yang ada saat ia dimulai -- perilaku yang
            // bisa dijelaskan.
            var rows = view.Cast<DataRowView>().ToList();

            context.SetValue(_rows, rows);
            context.SetValue(_next, 0);
            SetTotal(context, rows.Count);

            ScheduleNext(context);
        }

        private void OnBodyComplete(NativeActivityContext context, ActivityInstance completed)
        {
            if (BreakRequested) return;
            ScheduleNext(context);
        }

        private void ScheduleNext(NativeActivityContext context)
        {
            if (Body == null || Body.Handler == null) return;

            var rows = _rows.Get(context);
            if (rows == null) return;

            var next = _next.Get(context);
            if (next >= rows.Count) return;

            context.SetValue(_next, next + 1);
            IncrementIndex(context);
            context.ScheduleAction(Body, rows[next], OnBodyComplete);
        }

        /// <summary>
        /// Bentuk awal saat activity ditarik dari toolbox: variabel Index dan
        /// Total sudah ada, dan argumen badannya bernama "row" — supaya orang
        /// bisa langsung menulis row("NamaKolom") tanpa menyiapkan apa pun.
        /// </summary>
        public Activity Create(DependencyObject target)
        {
            var loop = new ForEachDataRow();

            loop.Variables.Add(new Variable<int>("Index", 0));
            loop.Variables.Add(new Variable<int>("Total", 0));

            loop.Body = new ActivityAction<DataRowView>
            {
                Argument = new DelegateInArgument<DataRowView>("row"),
            };

            return loop;
        }
    }

    // ==================================================================

    /// <summary>
    /// Ulangi untuk setiap butir sebuah koleksi.
    ///
    /// Pengganti OpenRPA.Activities.ForEachOf. Berbeda dari For Each bawaan WF,
    /// yang ini bisa dihentikan dengan Break dan punya Index serta Total.
    /// </summary>
    [Designer(typeof(ForEachOfDesigner))]
    [ContentProperty("Body")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.foreachof.png")]
    [DisplayName("For Each Of")]
    [Description("Ulangi isi badan untuk setiap butir koleksi, dengan Break dan Index.")]
    public class ForEachOf<T> : JakForgeLoop, IActivityTemplateFactory
    {
        [RequiredArgument, Category("Input")]
        [Description("Koleksi yang diulang.")]
        public InArgument<IEnumerable<T>> Values { get; set; }

        [Browsable(false)]
        public ActivityAction<T> Body { get; set; }

        private readonly Variable<List<T>> _items = new Variable<List<T>>("_items");
        private readonly Variable<int> _next = new Variable<int>("_next");

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);

            metadata.AddImplementationVariable(_items);
            metadata.AddImplementationVariable(_next);
        }

        protected override void StartLoop(NativeActivityContext context)
        {
            var source = Values.Get(context);

            if (source == null)
                throw new InvalidOperationException("For Each Of: Values kosong.");

            context.SetValue(_items, source.ToList());
            context.SetValue(_next, 0);
            SetTotal(context, _items.Get(context).Count);

            ScheduleNext(context);
        }

        private void OnBodyComplete(NativeActivityContext context, ActivityInstance completed)
        {
            if (BreakRequested) return;
            ScheduleNext(context);
        }

        private void ScheduleNext(NativeActivityContext context)
        {
            if (Body == null || Body.Handler == null) return;

            var items = _items.Get(context);
            if (items == null) return;

            var next = _next.Get(context);
            if (next >= items.Count) return;

            context.SetValue(_next, next + 1);
            IncrementIndex(context);
            context.ScheduleAction(Body, items[next], OnBodyComplete);
        }

        public Activity Create(DependencyObject target)
        {
            var loop = new ForEachOf<T>();

            loop.Variables.Add(new Variable<int>("Index", 0));
            loop.Variables.Add(new Variable<int>("Total", 0));

            loop.Body = new ActivityAction<T> { Argument = new DelegateInArgument<T>("item") };

            return loop;
        }
    }

    // ==================================================================

    /// <summary>
    /// Ulangi selama kondisinya benar, dan bisa dihentikan dengan Break.
    /// </summary>
    [Designer(typeof(BreakableWhileDesigner))]
    [ContentProperty("Body")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.breakablewhile.png")]
    [DisplayName("While")]
    [Description("Ulangi selama kondisinya benar. Bisa dihentikan dengan Break.")]
    public class BreakableWhile : JakForgeLoop
    {
        [RequiredArgument, Category("Input")]
        public Activity<bool> Condition { get; set; }

        [Browsable(false)]
        public Activity Body { get; set; }

        private readonly Variable<bool> _condition = new Variable<bool>("_condition");

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);
            metadata.AddImplementationVariable(_condition);
        }

        protected override void StartLoop(NativeActivityContext context)
        {
            Evaluate(context);
        }

        private void Evaluate(NativeActivityContext context)
        {
            if (Condition == null) return;
            context.ScheduleActivity(Condition, OnConditionComplete);
        }

        private void OnConditionComplete(NativeActivityContext context, ActivityInstance completed, bool result)
        {
            if (!result || BreakRequested) return;
            if (Body == null) return;

            IncrementIndex(context);
            context.ScheduleActivity(Body, OnBodyComplete);
        }

        private void OnBodyComplete(NativeActivityContext context, ActivityInstance completed)
        {
            if (BreakRequested) return;
            Evaluate(context);
        }
    }

    // ==================================================================

    /// <summary>
    /// Kerjakan badan sekali dulu, lalu ulangi selama kondisinya benar.
    /// </summary>
    [Designer(typeof(BreakableDoWhileDesigner))]
    [ContentProperty("Body")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.breakabledowhile.png")]
    [DisplayName("Do While")]
    [Description("Kerjakan badan sekali, lalu ulangi selama kondisinya benar. Bisa dihentikan dengan Break.")]
    public class BreakableDoWhile : JakForgeLoop
    {
        [RequiredArgument, Category("Input")]
        public Activity<bool> Condition { get; set; }

        [Browsable(false)]
        public Activity Body { get; set; }

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);
        }

        protected override void StartLoop(NativeActivityContext context)
        {
            // Bedanya dengan While hanya di sini: badannya dijalankan lebih dulu,
            // baru kondisinya diperiksa.
            if (Body == null) return;

            IncrementIndex(context);
            context.ScheduleActivity(Body, OnBodyComplete);
        }

        private void OnBodyComplete(NativeActivityContext context, ActivityInstance completed)
        {
            if (BreakRequested || Condition == null) return;
            context.ScheduleActivity(Condition, OnConditionComplete);
        }

        private void OnConditionComplete(NativeActivityContext context, ActivityInstance completed, bool result)
        {
            if (!result || BreakRequested || Body == null) return;

            IncrementIndex(context);
            context.ScheduleActivity(Body, OnBodyComplete);
        }
    }
}
