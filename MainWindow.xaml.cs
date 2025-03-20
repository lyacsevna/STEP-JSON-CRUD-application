
using Newtonsoft.Json;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

using System.Windows.Shapes;

namespace STEP_JSON_Application_for_ASKON
{
   
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            ViewButton.IsChecked = true;
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";

            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;

                if (IsValidJson(filePath))
                {
                    string fileContent = File.ReadAllText(filePath);

                    SelectFileTextBlock.Visibility = Visibility.Collapsed;

                    StepJsonTextBox.Text = fileContent;

                    LoadedFilesListBox.Items.Add(System.IO.Path.GetFileName(filePath));
                }
                else
                {
                    MessageBox.Show("Файл не является валидным JSON.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private bool IsValidJson(string filePath)
        {
            try
            {
                var jsonString = File.ReadAllText(filePath);
                JsonConvert.DeserializeObject(jsonString);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Произошла ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
        private void ViewButton_Checked(object sender, RoutedEventArgs e)
        {
            EditorButton.IsChecked = false;
            StepJsonTextBox.IsReadOnly = true;

        }

        private void EditorButton_Checked(object sender, RoutedEventArgs e)
        {
            ViewButton.IsChecked = false;
            StepJsonTextBox.IsReadOnly = false;
        }

    }
}
