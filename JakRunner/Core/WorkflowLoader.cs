using System.IO;
using System.Collections.Generic;
using System;
using System.Activities;
using System.Activities.XamlIntegration;
using System.Xaml;

namespace JakRunner.Core
{
    /// <summary>
    /// Pemuat berkas .xaml JakRunner.
    ///
    /// Sama seperti ActivityXamlServices.Load biasa, kecuali satu hal: nama tipe
    /// activity milik Studio lama dialihkan ke padanan JakForge.
    /// </summary>
    public static class WorkflowLoader
    {
        public static Activity Load(string path)
        {
            EnsurePresentationTypesLoaded();

            var settings = new XamlXmlReaderSettings
            {
                // Berkas dari Studio memakai xmlns:this="clr-namespace:" untuk
                // menyebut dirinya sendiri; tanpa LocalAssembly, rujukan itu
                // tidak punya rakitan untuk dicari.
                LocalAssembly = typeof(WorkflowLoader).Assembly,
            };

            // Aliran berkasnya DIBUKA SENDIRI, bukan diserahkan ke overload
            // XamlXmlReader(path, ...).
            //
            // Overload itu membuka berkasnya sendiri dan TIDAK menutupnya saat
            // reader-nya dibuang -- berkasnya baru terlepas ketika pengumpul
            // sampah kebetulan berjalan. Diuji: sesudah Load selesai, membuka
            // berkas yang sama untuk ditulis gagal dengan "used by another
            // process", dan baru berhasil sesudah GC.Collect().
            //
            // Akibatnya nyata: JakRunner yang pernah memuat sebuah proyek
            // menahan berkas .xaml-nya, sehingga penerbitan ulang dari Studio
            // bisa gagal karena berkasnya sedang dipakai.
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new XamlXmlReader(stream, new RunnerSchemaContext(), settings))
            {
                return ActivityXamlServices.Load(reader);
            }
        }

        /// <summary>
        /// Pastikan System.Activities.Presentation sudah dimuat.
        ///
        /// Berkas yang disimpan Studio memuat anotasi — keterangan yang ditempel
        /// pada activity — lewat namespace XML
        /// "…/2010/xaml/activities/presentation". Namespace berbentuk URI seperti
        /// itu hanya bisa dipetakan dari rakitan yang SUDAH ADA di AppDomain;
        /// tidak seperti "clr-namespace:…;assembly=…", ia tidak menyebutkan
        /// rakitan mana yang harus dimuat.
        ///
        /// Studio memuat rakitan itu karena ia memang menggambar kanvas.
        /// JakRunner tidak pernah menyentuhnya, sehingga setiap workflow yang
        /// punya anotasi gagal dimuat dengan "Cannot set unknown member
        /// Annotation.AnnotationText" — termasuk seluruh template ReFramework,
        /// yang setiap state-nya beranotasi.
        ///
        /// Menyentuh satu tipe di dalamnya sudah cukup untuk memuatnya.
        /// </summary>
        private static void EnsurePresentationTypesLoaded()
        {
            if (_presentationLoaded) return;

            try
            {
                var annotation = typeof(System.Activities.Presentation.Annotations.Annotation);
                _presentationLoaded = annotation != null;
            }
            catch (Exception)
            {
                // Kalau rakitannya benar-benar tidak ada, pemuatan workflow
                // beranotasi akan gagal dengan pesannya sendiri — dan itu lebih
                // jelas daripada kegagalan di sini.
                _presentationLoaded = true;
            }
        }

        private static bool _presentationLoaded;
    }

    /// <summary>
    /// Konteks skema yang memetakan tipe Studio lama ke padanan JakForge.
    ///
    /// Proyek yang dibuat sebelum penggantian menuliskan
    /// OpenRPA.Activities.InvokeOpenRPA di berkas XAML-nya. Tipe itu hidup di
    /// dalam Studio dan Execute-nya bergantung pada runtime Studio, jadi
    /// JakRunner tidak bisa memuatnya — dan proyek yang sudah jadi akan rusak
    /// hanya karena kami mengganti nama.
    ///
    /// Pemetaan dilakukan di lapisan skema, bukan dengan menyunting teks XAML
    /// sebelum dibaca. Menyunting teks berarti menebak bentuk berkas — prefiks
    /// namespace bisa apa saja, atributnya bisa berpindah baris — dan tebakan
    /// yang meleset merusak berkas yang sebetulnya sah.
    ///
    /// Custom.Orchestrator.Activities.InvokeWorkflow menyediakan nama properti
    /// lama (workflow, WaitForCompleted, KillIfRunning) sebagai alias, sehingga
    /// atribut di berkas lama tetap terbaca.
    /// </summary>
    public class RunnerSchemaContext : XamlSchemaContext
    {
        private const string StudioActivities = "clr-namespace:OpenRPA.Activities;assembly=OpenRPA";

        /// <summary>
        /// Nama tipe lama, dan padanan JakForge-nya.
        ///
        /// Semua tipe di sebelah kiri hidup di dalam rakitan Studio, sehingga
        /// JakRunner tidak bisa memuatnya sama sekali — workflow yang memakainya
        /// gagal dengan "Cannot create unknown type", padahal jalan normal di
        /// kanvas Studio. Yang di sebelah kanan adalah tulisan ulang yang berdiri
        /// di atas runtime WF biasa.
        ///
        /// Bentuk propertinya dibuat sama, jadi berkas XAML lama tidak perlu
        /// disunting sama sekali.
        /// </summary>
        private static readonly Dictionary<string, Type> Replacements =
            new Dictionary<string, Type>(StringComparer.Ordinal)
            {
                { "InvokeOpenRPA",    typeof(Custom.Orchestrator.Activities.InvokeWorkflow) },
                { "ForEachDataRow",   typeof(Custom.Flow.Activities.ForEachDataRow) },
                { "BreakableWhile",   typeof(Custom.Flow.Activities.BreakableWhile) },
                { "BreakableDoWhile", typeof(Custom.Flow.Activities.BreakableDoWhile) },
                { "Break",            typeof(Custom.Flow.Activities.Break) },
                { "Continue",         typeof(Custom.Flow.Activities.Continue) },
                { "CommentOut",       typeof(Custom.Flow.Activities.CommentOut) },
            };

        protected override XamlType GetXamlType(string xamlNamespace, string name, params XamlType[] typeArguments)
        {
            var substitute = Substitute(xamlNamespace, name, typeArguments);
            return substitute ?? base.GetXamlType(xamlNamespace, name, typeArguments);
        }

        private XamlType Substitute(string xamlNamespace, string name, XamlType[] typeArguments)
        {
            if (!string.Equals(xamlNamespace, StudioActivities, StringComparison.OrdinalIgnoreCase)) return null;

            // ForEachOf bertipe generik: namanya di XAML tertulis "ForEachOf"
            // dengan x:TypeArguments terpisah, jadi tipe penggantinya harus
            // dibentuk dengan argumen tipe yang sama.
            if (string.Equals(name, "ForEachOf", StringComparison.Ordinal)
                && typeArguments != null && typeArguments.Length == 1)
            {
                var closed = typeof(Custom.Flow.Activities.ForEachOf<>)
                    .MakeGenericType(typeArguments[0].UnderlyingType);

                return GetXamlType(closed);
            }

            Type replacement;
            return Replacements.TryGetValue(name, out replacement) ? GetXamlType(replacement) : null;
        }
    }
}
