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

            // Отображаем различия
            ShowDiff(file1Content, file2Content);
        }

        private void ShowDiff(string file1Content, string file2Content)
        {
            File1RichTextBox.Document.Blocks.Clear();
            File2RichTextBox.Document.Blocks.Clear();

            // Создаем объект для сравнения
            var diffBuilder = new InlineDiffBuilder(new Differ());
            var diff = diffBuilder.BuildDiffModel(file1Content, file2Content);

            // Заполняем RichTextBox для файла 1
            foreach (var line in diff.Lines)
            {
                Paragraph paragraph = new Paragraph();
                Run run = new Run(line.Text);

                // Проверяем тип строки и задаем цвет
                if (line.Type == ChangeType.Inserted)
                {
                    run.Foreground = System.Windows.Media.Brushes.Green; // Вставленные строки - зеленые
                }
                else if (line.Type == ChangeType.Deleted)
                {
                    run.Foreground = System.Windows.Media.Brushes.Red; // Удаленные строки - красные
                }

                paragraph.Inlines.Add(run);
                File1RichTextBox.Document.Blocks.Add(paragraph);
            }

            // Заполняем RichTextBox для файла 2
            foreach (var line in diff.Lines)
            {
                Paragraph paragraph = new Paragraph();
                Run run = new Run(line.Text);

                // Проверяем тип строки и задаем цвет
                if (line.Type == ChangeType.Inserted)
                {
                    run.Foreground = System.Windows.Media.Brushes.Green; // Вставленные строки - зеленые
                }
                else if (line.Type == ChangeType.Deleted)
                {
                    run.Foreground = System.Windows.Media.Brushes.Red; // Удаленные строки - красные
                }

                paragraph.Inlines.Add(run);
                File2RichTextBox.Document.Blocks.Add(paragraph);
            }
        }
    }
}