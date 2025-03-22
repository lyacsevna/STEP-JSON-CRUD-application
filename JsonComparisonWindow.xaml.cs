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
        public JsonComparisonWindow(string file1Content, string file2Content)
        {
            InitializeComponent();
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
                Paragraph paragraph = new Paragraph();
                Run run = new Run(line.Text);

                if (line.Type == ChangeType.Inserted)
                {
                    run.Foreground = System.Windows.Media.Brushes.Green;
                }
                else if (line.Type == ChangeType.Deleted)
                {
                    run.Foreground = System.Windows.Media.Brushes.Red;
                }

                paragraph.Inlines.Add(run);
                File1RichTextBox.Document.Blocks.Add(paragraph);
            }

            foreach (var line in diff.Lines)
            {
                Paragraph paragraph = new Paragraph();
                Run run = new Run(line.Text);

                if (line.Type == ChangeType.Inserted)
                {
                    run.Foreground = System.Windows.Media.Brushes.Green;
                }
                else if (line.Type == ChangeType.Deleted)
                {
                    run.Foreground = System.Windows.Media.Brushes.Red;
                }

                paragraph.Inlines.Add(run);
                File2RichTextBox.Document.Blocks.Add(paragraph);
            }
        }
    }
}