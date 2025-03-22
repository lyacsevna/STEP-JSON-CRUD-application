using System.Windows;
using System.Windows.Documents;
using System.Windows.Controls;
using System.Windows.Media;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;

namespace STEP_JSON_Application_for_ASKON
{
    public partial class JsonComparisonWindow : Window
    {
        public string FileName1 { get; set; }
        public string FileName2 { get; set; }

        public JsonComparisonWindow(string file1Content, string file2Content, string fileName1, string fileName2)
        {
            InitializeComponent();
            FileName1 = fileName1;
            FileName2 = fileName2;
            DataContext = this;
            ShowDiff(file1Content, file2Content);
        }

        private void ShowDiff(string file1Content, string file2Content)
        {
            File1RichTextBox.Document.Blocks.Clear();
            File2RichTextBox.Document.Blocks.Clear();

            var diffBuilder = new InlineDiffBuilder(new Differ());
            var diff = diffBuilder.BuildDiffModel(file1Content, file2Content);

            foreach (var line in diff.Lines)
            {
                Paragraph paragraph1 = new Paragraph();
                Run run1 = new Run(line.Text);

                if (line.Type == ChangeType.Inserted)
                {
                    run1.Foreground = Brushes.Green;
                }
                else if (line.Type == ChangeType.Deleted)
                {
                    run1.Foreground = Brushes.Red;
                }

                paragraph1.Inlines.Add(run1);
                File1RichTextBox.Document.Blocks.Add(paragraph1);
            }

            foreach (var line in diff.Lines)
            {
                Paragraph paragraph2 = new Paragraph();
                Run run2 = new Run(line.Text);

                if (line.Type == ChangeType.Inserted)
                {
                    run2.Foreground = Brushes.Green;
                }
                else if (line.Type == ChangeType.Deleted)
                {
                    run2.Foreground = Brushes.Red;
                }

                paragraph2.Inlines.Add(run2);
                File2RichTextBox.Document.Blocks.Add(paragraph2);
            }
        }
    }
}