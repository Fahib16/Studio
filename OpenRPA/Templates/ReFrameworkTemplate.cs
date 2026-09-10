using System;
using System.Activities;
using System.Activities.Statements;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xaml;
using Microsoft.VisualBasic.Activities;
using OpenRPA.Interfaces;

namespace OpenRPA.Templates
{
    /// <summary>
    /// Template Robotic Enterprise Framework (ReFramework) untuk JakForge.
    ///
    /// XAML-nya TIDAK ditulis sebagai teks di dalam kode. Pohon activity-nya
    /// dibangun sebagai objek lalu diserialkan oleh WF sendiri. Alasannya
    /// sederhana: XAML workflow yang ditulis tangan gampang salah satu tanda,
    /// dan salahnya baru ketahuan saat berkasnya dibuka — sebagai kotak merah
    /// di kanvas, bukan sebagai kesalahan yang bisa dibaca. Dengan dibangun
    /// dari objek, yang bisa salah hanya kodenya, dan itu ketahuan saat
    /// dikompilasi.
    ///
    /// Bedanya dengan ReFramework UiPath: berkas setelan memakai JSON, bukan
    /// Excel. Isinya sama saja, tapi tidak menuntut Excel terpasang, bisa
    /// dibaca dan di-diff apa adanya, dan dibaca cukup dengan satu Assign
    /// tanpa activity tambahan.
    /// </summary>
    public static class ReFrameworkTemplate
    {
        public const string TemplateName = "Robotic Enterprise Framework";

        public const string TemplateDescription =
            "Kerangka kerja transaksional: state machine Initialization, Get Transaction Data, " +
            "Process Transaction, dan End Process, lengkap dengan setelan terpusat, percobaan ulang, " +
            "serta pemisahan Business Exception dan System Exception.";

        /// <summary>Nama berkas yang dihasilkan, berurutan seperti mau dibuka orang.</summary>
        public static readonly string[] WorkflowNames =
        {
            "Main",
            "InitAllSettings",
            "InitAllApplications",
            "GetTransactionData",
            "Process",
            "SetTransactionStatus",
            "CloseAllApplications",
        };

        // ------------------------------------------------------------------
        // Titik masuk
        // ------------------------------------------------------------------

        /// <summary>
        /// Tuliskan seluruh berkas template ke <paramref name="folder"/>.
        /// Mengembalikan daftar path berkas .xaml yang dibuat, berurutan.
        /// </summary>
        public static List<string> WriteTo(string folder, string projectFolderName)
        {
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            var dataFolder = Path.Combine(folder, "Data");
            if (!Directory.Exists(dataFolder)) Directory.CreateDirectory(dataFolder);

            File.WriteAllText(Path.Combine(dataFolder, "Config.json"), ConfigJson(), Encoding.UTF8);
            File.WriteAllText(Path.Combine(folder, "README-ReFramework.md"), Readme(), Encoding.UTF8);

            var created = new List<string>();

            foreach (var name in WorkflowNames)
            {
                var builder = BuildWorkflow(name, projectFolderName);
                var path = Path.Combine(folder, name + ".xaml");

                File.WriteAllText(path, Serialize(builder), Encoding.UTF8);
                created.Add(path);
            }

            return created;
        }

        private static ActivityBuilder BuildWorkflow(string name, string projectFolderName)
        {
            switch (name)
            {
                case "Main": return Main(projectFolderName);
                case "InitAllSettings": return InitAllSettings();
                case "InitAllApplications": return InitAllApplications();
                case "GetTransactionData": return GetTransactionData();
                case "Process": return Process();
                case "SetTransactionStatus": return SetTransactionStatus();
                case "CloseAllApplications": return CloseAllApplications();
                default: throw new ArgumentException("Workflow template tidak dikenal: " + name);
            }
        }

        // ------------------------------------------------------------------
        // Serialisasi
        // ------------------------------------------------------------------

        /// <summary>
        /// ActivityBuilder menjadi XAML yang bisa dibuka WF Designer.
        ///
        /// CreateBuilderWriter WAJIB dipakai. Tanpa itu yang keluar XAML sebuah
        /// Activity biasa, bukan &lt;Activity x:Class=...&gt; dengan x:Members —
        /// dan berkas seperti itu kehilangan seluruh argumen workflow-nya saat
        /// dibuka.
        /// </summary>
        private static string Serialize(ActivityBuilder builder)
        {
            var text = new StringBuilder();

            var settings = new System.Xml.XmlWriterSettings
            {
                Indent = true,
                OmitXmlDeclaration = true,
                NewLineOnAttributes = false,
            };

            using (var xmlWriter = System.Xml.XmlWriter.Create(text, settings))
            using (var xamlWriter = System.Activities.XamlIntegration.ActivityXamlServices.CreateBuilderWriter(
                       new XamlXmlWriter(xmlWriter, new XamlSchemaContext())))
            {
                XamlServices.Save(xamlWriter, builder);
            }

            return text.ToString();
        }

        /// <summary>
        /// Bungkus sebuah implementasi menjadi ActivityBuilder lengkap dengan
        /// namespace Visual Basic yang dibutuhkan ekspresinya.
        /// </summary>
        private static ActivityBuilder Wrap(string name, Activity implementation,
                                            params DynamicActivityProperty[] properties)
        {
            var builder = new ActivityBuilder
            {
                Name = name.Replace(" ", "_"),
                Implementation = implementation,
            };

            foreach (var property in properties) builder.Properties.Add(property);

            WFHelper.AddVBNamespaceSettings(builder, new string[] { },
                typeof(Action),
                typeof(System.Xml.XmlNode),
                typeof(System.Data.DataSet),
                typeof(System.Linq.Enumerable),
                typeof(Microsoft.VisualBasic.Collection),
                typeof(System.Data.DataTableExtensions),
                typeof(Newtonsoft.Json.JsonConvert),
                typeof(Dictionary<string, object>),
                typeof(File),
                typeof(Custom.Orchestrator.Runtime.QueueItem));

            return builder;
        }

        private static DynamicActivityProperty In<T>(string name)
        {
            return new DynamicActivityProperty
            {
                Name = name,
                Type = typeof(InArgument<T>),
                Value = new InArgument<T>(),
            };
        }

        private static DynamicActivityProperty Out<T>(string name)
        {
            return new DynamicActivityProperty
            {
                Name = name,
                Type = typeof(OutArgument<T>),
                Value = new OutArgument<T>(),
            };
        }

        private static DynamicActivityProperty InOut<T>(string name)
        {
            return new DynamicActivityProperty
            {
                Name = name,
                Type = typeof(InOutArgument<T>),
                Value = new InOutArgument<T>(),
            };
        }

        /// <summary>Ekspresi Visual Basic bertipe, dipakai di kondisi dan Assign.</summary>
        private static VisualBasicValue<T> Vb<T>(string expression)
        {
            return new VisualBasicValue<T>(expression);
        }

        private static Assign Set<T>(string to, string valueExpression)
        {
            return new Assign
            {
                To = new OutArgument<T>(new VisualBasicReference<T>(to)),
                Value = new InArgument<T>(Vb<T>(valueExpression)),
            };
        }


        /// <summary>
        /// Pasang keterangan pada sebuah activity — teks yang tampil di dalam
        /// kotaknya di kanvas.
        ///
        /// Keterangan di WF bukan properti biasa melainkan ATTACHABLE MEMBER
        /// (<c>sap2010:Annotation.AnnotationText</c>). Ia tidak bisa disetel
        /// lewat properti karena State bukan DependencyObject; jalannya lewat
        /// AttachablePropertyServices, dan penyerial XAML yang menuliskannya.
        ///
        /// Keterangan inilah yang membuat template ini bisa dibaca tanpa
        /// membuka satu per satu isinya: tiap keadaan menjelaskan sendiri apa
        /// tugasnya.
        /// </summary>
        private static T Note<T>(T activity, string text) where T : class
        {
            try
            {
                System.Xaml.AttachablePropertyServices.SetProperty(
                    activity,
                    new System.Xaml.AttachableMemberIdentifier(
                        typeof(System.Activities.Presentation.Annotations.Annotation), "AnnotationText"),
                    text);
            }
            catch (Exception ex)
            {
                // Keterangan hanya penjelasan; kegagalannya tidak boleh
                // menggagalkan pembuatan template.
                Log.Warning("Template: gagal memasang keterangan: " + ex.Message);
            }

            return activity;
        }

        /// <summary>Catatan di kanvas: satu langkah yang jelas terlihat dan aman dihapus.</summary>
        private static Activity Note(string text)
        {
            return new WriteLine
            {
                DisplayName = text,
                Text = new InArgument<string>(text),
            };
        }

        /// <summary>
        /// Panggilan ke sub-workflow.
        ///
        /// Memakai Invoke Workflow milik JakForge, bukan Invoke OpenRPA yang
        /// lama. Yang lama hanya bisa dijalankan dari dalam Studio, sehingga
        /// proyek ReFramework — yang seluruhnya dirangkai dari sub-workflow —
        /// tidak bisa dijalankan JakRunner sama sekali.
        ///
        /// Nama berkasnya ditulis TANPA awalan nama proyek. Yang lama
        /// menuliskan "ReFramework/Process.xaml", padahal berkasnya ada langsung
        /// di folder proyek; awalan itu hanya menambah satu hal yang bisa salah
        /// saat proyeknya disalin atau diganti nama.
        /// </summary>
        private static Activity Invoke(string projectFolderName, string workflowFile, string displayName)
        {
            return new Custom.Orchestrator.Activities.InvokeWorkflow
            {
                DisplayName = displayName,
                WorkflowFile = new InArgument<string>(workflowFile),
            };
        }

        // ------------------------------------------------------------------
        // Tata letak kanvas
        // ------------------------------------------------------------------

        /// <summary>
        /// Tempatkan sebuah State pada koordinat tertentu di kanvas.
        ///
        /// Tanpa ini, perancang StateMachine menumpuk semua state di sudut yang
        /// sama dan merutekan garis transisi di antaranya — hasilnya kotak yang
        /// saling menimpa dengan label transisi bertindihan, persis yang terlihat
        /// saat template ini pertama dibuka.
        ///
        /// Perancang menyimpan letak di kamus ViewState yang ditempelkan ke tiap
        /// objek; kuncinya "ShapeLocation" dan "ShapeSize". Nama itu yang dibaca
        /// StateContainerEditor saat kanvas digambar.
        /// </summary>
        private static T Place<T>(T shape, double x, double y, double width = 300, double height = 120)
            where T : class
        {
            var viewState = new Dictionary<string, object>
            {
                ["ShapeLocation"] = new System.Windows.Point(x, y),
                ["ShapeSize"] = new System.Windows.Size(width, height),
            };

            System.Activities.Presentation.View.WorkflowViewStateService.SetViewState(shape, viewState);

            // Catatan: VirtualizedContainerService.SetHintSize TIDAK dipakai di
            // sini. Menyetelnya pada State membuat perancang StateMachine tidak
            // menggambar satu pun kotak — kanvas hanya menyisakan bulatan Start.
            // ShapeSize saja sudah cukup, dan itu yang memang dibaca
            // StateContainerEditor saat menata isi kanvas.

            return shape;
        }

        // ------------------------------------------------------------------
        // Main: state machine
        // ------------------------------------------------------------------

        private static ActivityBuilder Main(string projectFolderName)
        {
            var config = new Variable<Dictionary<string, object>>("Config");
            var transactionItem = new Variable<Custom.Orchestrator.Runtime.QueueItem>("TransactionItem");
            var transactionNumber = new Variable<int>("TransactionNumber") { Default = 1 };
            var systemException = new Variable<Exception>("SystemException");
            var businessException = new Variable<Exception>("BusinessException");
            var retryNumber = new Variable<int>("RetryNumber") { Default = 0 };
            var consecutiveErrors = new Variable<int>("ConsecutiveSystemExceptions") { Default = 0 };

            // ---- Initialization ----
            var initialization = Note(new State
            {
                DisplayName = "Inisialisasi",
                Entry = new TryCatch
                {
                    DisplayName = "Init",
                    Try = new Sequence
                    {
                        DisplayName = "Siapkan setelan dan aplikasi",
                        Activities =
                        {
                            Invoke(projectFolderName, "InitAllSettings.xaml", "Init All Settings"),
                            Invoke(projectFolderName, "InitAllApplications.xaml", "Init All Applications"),
                            Set<Exception>("SystemException", "Nothing"),
                        },
                    },
                    Catches =
                    {
                        new Catch<Exception>
                        {
                            Action = new ActivityAction<Exception>
                            {
                                Argument = new DelegateInArgument<Exception>("ex"),
                                Handler = new Sequence
                                {
                                    DisplayName = "Gagal inisialisasi",
                                    Activities =
                                    {
                                        Set<Exception>("SystemException", "ex"),
                                    },
                                },
                            },
                        },
                    },
                },
            }, "Initialization — membaca berkas setelan dan menyiapkan aplikasi yang dipakai proses ini.");

            // ---- Get Transaction Data ----
            var getData = Note(new State
            {
                DisplayName = "Ambil",
                Entry = Invoke(projectFolderName, "GetTransactionData.xaml", "Get Transaction Data"),
            }, "Get Transaction Data — mengambil transaksi berikutnya yang harus dikerjakan. "
             + "Kalau Config berisi OrchestratorQueueName, transaksinya diambil dari antrean ForgeHub.");

            // ---- Process Transaction ----
            var process = Note(new State
            {
                DisplayName = "Proses",
                Entry = new Sequence
                {
                    DisplayName = "Kerjakan satu transaksi",
                    Activities =
                    {
                        new TryCatch
                        {
                            DisplayName = "Process",
                            Try = new Sequence
                            {
                                Activities =
                                {
                                    Set<Exception>("SystemException", "Nothing"),
                                    Set<Exception>("BusinessException", "Nothing"),
                                    Invoke(projectFolderName, "Process.xaml", "Process"),
                                },
                            },
                            Catches =
                            {
                                new Catch<BusinessRuleException>
                                {
                                    Action = new ActivityAction<BusinessRuleException>
                                    {
                                        Argument = new DelegateInArgument<BusinessRuleException>("bex"),
                                        Handler = Set<Exception>("BusinessException", "bex"),
                                    },
                                },
                                new Catch<Exception>
                                {
                                    Action = new ActivityAction<Exception>
                                    {
                                        Argument = new DelegateInArgument<Exception>("ex"),
                                        Handler = Set<Exception>("SystemException", "ex"),
                                    },
                                },
                            },
                        },
                        Invoke(projectFolderName, "SetTransactionStatus.xaml", "Set Transaction Status"),
                    },
                },
            }, "Process Transaction — mengerjakan SATU transaksi. Hasilnya salah satu dari: 1) Berhasil, "
             + "2) Business Exception, 3) System Exception. Pada System Exception, "
             + "transaksinya dicoba lagi secara otomatis.");

            // ---- End Process ----
            var endProcess = Note(new State
            {
                DisplayName = "Selesai",
                IsFinal = true,
                Entry = Invoke(projectFolderName, "CloseAllApplications.xaml", "Close All Applications"),
            }, "End Process — mengakhiri proses dan menutup semua aplikasi yang tadi dipakai.");

            // ---- Transisi ----
            initialization.Transitions.Add(new Transition
            {
                DisplayName = "Berhasil",
                Condition = Vb<bool>("SystemException Is Nothing"),
                To = getData,
            });
            initialization.Transitions.Add(new Transition
            {
                DisplayName = "Gagal inisialisasi",
                Condition = Vb<bool>("SystemException IsNot Nothing"),
                To = endProcess,
            });

            getData.Transitions.Add(new Transition
            {
                DisplayName = "Ada transaksi baru",
                Condition = Vb<bool>("TransactionItem IsNot Nothing"),
                To = process,
            });
            getData.Transitions.Add(new Transition
            {
                DisplayName = "Sudah habis",
                Condition = Vb<bool>("TransactionItem Is Nothing"),
                To = endProcess,
            });

            process.Transitions.Add(new Transition
            {
                DisplayName = "Selesai tanpa kesalahan",
                Condition = Vb<bool>("SystemException Is Nothing"),
                To = getData,
            });
            process.Transitions.Add(new Transition
            {
                DisplayName = "System Exception, ulangi dari awal",
                Condition = Vb<bool>("SystemException IsNot Nothing AndAlso ConsecutiveSystemExceptions < 3"),
                To = initialization,
            });
            process.Transitions.Add(new Transition
            {
                DisplayName = "Terlalu banyak kesalahan beruntun",
                Condition = Vb<bool>("SystemException IsNot Nothing AndAlso ConsecutiveSystemExceptions >= 3"),
                To = endProcess,
            });

            // Letak keempat state.
            //
            // Bukan satu kolom lurus: End Process digeser ke kanan supaya tiga
            // transisi yang menuju ke sana — dari Initialization, Get Transaction
            // Data, dan Process Transaction — tidak berdesakan di lorong yang
            // sama. Pada versi satu kolom, ketiganya berimpit dan label
            // kondisinya saling menimpa sampai tidak terbaca.
            //
            // Jarak vertikal 210 memberi ruang bagi label transisi untuk duduk
            // di antara dua kotak, bukan di atas salah satunya.
            Place(initialization, 380, 40);
            Place(getData,        380, 250);
            Place(process,        380, 460);
            Place(endProcess,     760, 690);

            var machine = Note(new StateMachine
            {
                DisplayName = "Robotic Enterprise Framework",
                InitialState = initialization,
                States = { initialization, getData, process, endProcess },
                Variables =
                {
                    config, transactionItem, transactionNumber,
                    systemException, businessException, retryNumber, consecutiveErrors,
                },
            }, "Robotic Enterprise Framework — kerangka kerja transaksional.\n\n"
             + "Isi keterangan ini dengan informasi proyek Anda: penulis, kontak, "
             + "aplikasi yang terlibat, dan persiapan di luar Studio yang dibutuhkan.");

            return Wrap("Main", machine);
        }

        // ------------------------------------------------------------------
        // Sub-workflow
        // ------------------------------------------------------------------

        private static ActivityBuilder InitAllSettings()
        {
            var body = new Sequence
            {
                DisplayName = "Init All Settings",
                Activities =
                {
                    // Config.json dicari di FOLDER PROYEK, bukan di sebelah
                    // program yang menjalankannya.
                    //
                    // Versi sebelumnya memakai Assembly.GetEntryAssembly().Location,
                    // yang menunjuk ke folder OpenRPA.exe atau JakRunner.exe —
                    // tempat yang tidak pernah memuat Data\Config.json. Akibatnya
                    // setiap proyek ReFramework gagal pada langkah pertama dengan
                    // FileNotFoundException, dan sebabnya tidak terbaca dari
                    // pesannya. WorkflowContext.ProjectFolder disetel Studio dan
                    // JakRunner sebelum workflow dimulai.
                    new Assign
                    {
                        DisplayName = "Baca Data\\Config.json",
                        To = new OutArgument<Dictionary<string, object>>(
                            new VisualBasicReference<Dictionary<string, object>>("Config")),
                        Value = new InArgument<Dictionary<string, object>>(
                            Vb<Dictionary<string, object>>(
                                "Newtonsoft.Json.JsonConvert.DeserializeObject(Of System.Collections.Generic.Dictionary(Of String, Object))(" +
                                "System.IO.File.ReadAllText(System.IO.Path.Combine(" +
                                "Custom.Orchestrator.Runtime.WorkflowContext.ProjectFolder, \"Data\", \"Config.json\")))")),
                    },
                    Note("Setelan sudah dimuat. Tambahkan pemeriksaan setelan wajib di sini kalau perlu."),
                },
            };

            return Wrap("InitAllSettings", body, Out<Dictionary<string, object>>("Config"));
        }

        private static ActivityBuilder InitAllApplications()
        {
            var body = new Sequence
            {
                DisplayName = "Init All Applications",
                Activities =
                {
                    Note("Buka aplikasi yang dibutuhkan di sini: Open Browser, Terminal Session, Database Scope, dan seterusnya."),
                },
            };

            return Wrap("InitAllApplications", body, In<Dictionary<string, object>>("Config"));
        }

        /// <summary>
        /// Ambil satu transaksi berikutnya.
        ///
        /// Kalau Config berisi OrchestratorQueueName, transaksinya diambil dari
        /// antrean ForgeHub — beberapa robot bisa mengerjakan antrean yang sama
        /// tanpa saling mengambil butir yang sama, karena ForgeHub menandai
        /// butir itu "sedang diproses" dalam langkah yang sama saat memberikannya.
        ///
        /// Kalau nama antreannya kosong, TransactionItem diisi Nothing dan
        /// prosesnya berhenti setelah satu putaran. Itu keadaan awal template:
        /// kerangkanya jalan, tapi belum ada yang dikerjakan sampai Anda mengisi
        /// nama antreannya atau mengganti bagian ini dengan sumber data lain.
        /// </summary>
        private static ActivityBuilder GetTransactionData()
        {
            var queueName = Vb<string>(
                "If(Config IsNot Nothing AndAlso Config.ContainsKey(\"OrchestratorQueueName\") "
                + "AndAlso Config(\"OrchestratorQueueName\") IsNot Nothing, "
                + "Config(\"OrchestratorQueueName\").ToString(), \"\")");

            var fromQueue = new Custom.Orchestrator.Activities.GetQueueItem
            {
                DisplayName = "Ambil butir dari antrean ForgeHub",
                QueueName = new InArgument<string>(queueName),
                Item = new OutArgument<Custom.Orchestrator.Runtime.QueueItem>(
                    new VisualBasicReference<Custom.Orchestrator.Runtime.QueueItem>("TransactionItem")),
            };

            var body = new Sequence
            {
                DisplayName = "Get Transaction Data",
                Activities =
                {
                    Note("Ambil satu transaksi berikutnya. Isi TransactionItem dengan Nothing kalau sudah habis."),
                    new If
                    {
                        DisplayName = "Pakai antrean ForgeHub?",
                        Condition = new InArgument<bool>(Vb<bool>(
                            "Config IsNot Nothing AndAlso Config.ContainsKey(\"OrchestratorQueueName\") "
                            + "AndAlso Not String.IsNullOrEmpty(Convert.ToString(Config(\"OrchestratorQueueName\")))")),
                        Then = fromQueue,
                        Else = new Assign
                        {
                            DisplayName = "Belum ada sumber data: hentikan setelah satu putaran",
                            To = new OutArgument<Custom.Orchestrator.Runtime.QueueItem>(
                                new VisualBasicReference<Custom.Orchestrator.Runtime.QueueItem>("TransactionItem")),
                            Value = new InArgument<Custom.Orchestrator.Runtime.QueueItem>(
                                (Activity<Custom.Orchestrator.Runtime.QueueItem>)
                                Vb<Custom.Orchestrator.Runtime.QueueItem>("Nothing")),
                        },
                    },
                },
            };

            return Wrap("GetTransactionData", body,
                In<Dictionary<string, object>>("Config"),
                InOut<int>("TransactionNumber"),
                Out<Custom.Orchestrator.Runtime.QueueItem>("TransactionItem"));
        }

        private static ActivityBuilder Process()
        {
            var body = new Sequence
            {
                DisplayName = "Process",
                Activities =
                {
                    Note("Kerjakan SATU transaksi di sini."),
                    Note("Lempar BusinessRuleException untuk kesalahan data, Exception biasa untuk kesalahan sistem."),
                },
            };

            return Wrap("Process", body,
                In<Custom.Orchestrator.Runtime.QueueItem>("TransactionItem"),
                In<Dictionary<string, object>>("Config"));
        }

        /// <summary>
        /// Laporkan hasil satu butir antrean ke ForgeHub, kalau memang ada butirnya.
        ///
        /// Dibungkus If karena TransactionItem berisi Nothing pada proyek yang
        /// belum memakai antrean. Tanpa penjagaan itu, template yang baru dibuat
        /// akan gagal pada putaran pertama — sebelum penggunanya sempat mengisi
        /// apa pun.
        /// </summary>
        private static Activity ReportQueueItem(string status, string errorExpression, string displayName)
        {
            var report = new Custom.Orchestrator.Activities.SetQueueItemStatus
            {
                DisplayName = displayName,
                Item = new InArgument<Custom.Orchestrator.Runtime.QueueItem>(
                    Vb<Custom.Orchestrator.Runtime.QueueItem>("TransactionItem")),
                Status = new InArgument<string>(status),
            };

            if (errorExpression != "Nothing")
            {
                report.ErrorMessage = new InArgument<string>(Vb<string>(errorExpression));
            }

            return new If
            {
                DisplayName = "Ada butir antrean?",
                Condition = new InArgument<bool>(Vb<bool>("TransactionItem IsNot Nothing")),
                Then = report,
            };
        }

        private static ActivityBuilder SetTransactionStatus()
        {
            var body = new Sequence
            {
                DisplayName = "Set Transaction Status",
                Activities =
                {
                    new If
                    {
                        DisplayName = "System Exception?",
                        Condition = new InArgument<bool>(Vb<bool>("SystemException IsNot Nothing")),
                        Then = new Sequence
                        {
                            DisplayName = "Kesalahan sistem",
                            Activities =
                            {
                                ReportQueueItem("FAILED", "SystemException.Message",
                                    "Laporkan gagal — ForgeHub akan mencoba lagi"),
                                Set<int>("ConsecutiveSystemExceptions", "ConsecutiveSystemExceptions + 1"),
                                Set<int>("RetryNumber", "RetryNumber + 1"),
                            },
                        },
                        Else = new If
                        {
                            DisplayName = "Business Exception?",
                            Condition = new InArgument<bool>(Vb<bool>("BusinessException IsNot Nothing")),
                            Then = new Sequence
                            {
                                DisplayName = "Kesalahan data: lanjut ke transaksi berikutnya",
                                Activities =
                                {
                                    // ABANDONED, bukan FAILED: data yang memang salah
                                    // tidak akan menjadi benar kalau dicoba lagi, dan
                                    // percobaan ulang hanya menunda pekerjaan berikutnya.
                                    ReportQueueItem("ABANDONED", "BusinessException.Message",
                                        "Laporkan ditinggalkan — data tidak memenuhi aturan"),
                                    Set<int>("ConsecutiveSystemExceptions", "0"),
                                    Set<int>("TransactionNumber", "TransactionNumber + 1"),
                                },
                            },
                            Else = new Sequence
                            {
                                DisplayName = "Berhasil",
                                Activities =
                                {
                                    ReportQueueItem("SUCCESSFUL", "Nothing", "Laporkan berhasil"),
                                    Set<int>("ConsecutiveSystemExceptions", "0"),
                                    Set<int>("RetryNumber", "0"),
                                    Set<int>("TransactionNumber", "TransactionNumber + 1"),
                                },
                            },
                        },
                    },
                },
            };

            return Wrap("SetTransactionStatus", body,
                In<Dictionary<string, object>>("Config"),
                In<Exception>("SystemException"),
                In<Exception>("BusinessException"),
                In<Custom.Orchestrator.Runtime.QueueItem>("TransactionItem"),
                InOut<int>("TransactionNumber"),
                InOut<int>("RetryNumber"),
                InOut<int>("ConsecutiveSystemExceptions"));
        }

        private static ActivityBuilder CloseAllApplications()
        {
            var body = new Sequence
            {
                DisplayName = "Close All Applications",
                Activities =
                {
                    Note("Tutup aplikasi yang tadi dibuka di sini, dalam urutan terbalik."),
                },
            };

            return Wrap("CloseAllApplications", body, In<Dictionary<string, object>>("Config"));
        }

        // ------------------------------------------------------------------
        // Berkas pendamping
        // ------------------------------------------------------------------

        private static string ConfigJson()
        {
            return string.Join(Environment.NewLine,
                "{",
                "  \"_catatan\": \"Setelan terpusat ReFramework. Dibaca InitAllSettings menjadi Config.\",",
                "",
                "  \"MaxRetryNumber\": 2,",
                "  \"MaxConsecutiveSystemExceptions\": 3,",
                "",
                "  \"LogMessage_BeginProcess\": \"Mulai memproses transaksi\",",
                "  \"LogMessage_EndProcess\": \"Selesai memproses transaksi\",",
                "  \"LogMessage_GetTransactionData\": \"Mengambil transaksi berikutnya\",",
                "",
                "  \"ExceptionScreenshotsFolderPath\": \"Exceptions_Screenshots\",",
                "",
                "  \"OrchestratorQueueName\": \"\",",
                "  \"OrchestratorQueueFolder\": \"\"",
                "}",
                "");
        }

        private static string Readme()
        {
            return string.Join(Environment.NewLine,
                "# Robotic Enterprise Framework (JakForge)",
                "",
                "Kerangka kerja transaksional. Main.xaml berisi state machine dengan empat keadaan:",
                "",
                "| Keadaan | Isinya |",
                "|---|---|",
                "| Initialization | Membaca setelan, membuka aplikasi |",
                "| Get Transaction Data | Mengambil satu transaksi berikutnya |",
                "| Process Transaction | Mengerjakan transaksi itu, lalu mencatat hasilnya |",
                "| End Process | Menutup aplikasi; keadaan akhir |",
                "",
                "## Yang perlu Anda isi",
                "",
                "Empat berkas ini memang dibiarkan kosong, tinggal diisi:",
                "",
                "1. **InitAllApplications.xaml** - buka aplikasi yang dibutuhkan.",
                "2. **GetTransactionData.xaml** - ambil satu transaksi. Isi `TransactionItem`",
                "   dengan `Nothing` kalau sudah habis; itulah yang menghentikan putarannya.",
                "3. **Process.xaml** - kerjakan SATU transaksi.",
                "4. **CloseAllApplications.xaml** - tutup aplikasi, urutan terbalik.",
                "",
                "## Dua jenis kesalahan",
                "",
                "Bedanya menentukan apa yang terjadi sesudahnya, jadi pilihlah dengan sadar:",
                "",
                "- **BusinessRuleException** - datanya yang salah, bukan sistemnya. Transaksi itu",
                "  dilewati dan robot lanjut ke transaksi berikutnya. Tidak diulang, karena",
                "  mengulang hal yang sama akan gagal lagi.",
                "- **Exception biasa** - sistemnya yang bermasalah (aplikasi tidak merespons,",
                "  jaringan putus). Robot kembali ke Initialization dan mencoba lagi, sampai",
                "  tiga kali beruntun.",
                "",
                "Melemparnya dari ekspresi Visual Basic:",
                "",
                "```vb",
                "Throw New OpenRPA.Interfaces.BusinessRuleException(\"NIP tidak ditemukan: \" & nip)",
                "```",
                "",
                "## Setelan",
                "",
                "`Data\\Config.json` dibaca InitAllSettings menjadi `Config`, sebuah",
                "`Dictionary(Of String, Object)`. Menambah setelan cukup menambah barisnya di",
                "berkas itu; tidak ada yang perlu diubah di workflow.",
                "",
                "Membacanya dari ekspresi:",
                "",
                "```vb",
                "Config(\"MaxRetryNumber\").ToString()",
                "```",
                "");
        }
    }
}
