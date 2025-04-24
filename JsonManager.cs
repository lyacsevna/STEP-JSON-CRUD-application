using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Windows;

namespace STEP_JSON_Application_for_ASKON
{
    public class JsonManager
    {


        private List<string> loadedFilePaths = new List<string>();
        private MainWindow mainWindow;
        private static readonly TreeManager treeManager = new TreeManager();
        private static readonly SchemaManager schemaManager = new SchemaManager();

        public JsonManager(/*MainWindow mainWindow*/)
        {
            //this.mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }

        public void SelectJsonFile()
        {
            string filePath = GetJsonFilePath();

            if (!string.IsNullOrEmpty(filePath))
            {
                IsFileAlreadyLoaded(filePath);
            }
            else
            {
                MessageBox.Show("Файл не был выбран.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

        }

        public string GetJsonFilePath()
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                return openFileDialog.FileName;
            }
            else return null;
        }

        private void IsFileAlreadyLoaded(string filePath)
        {
            if (loadedFilePaths.Contains(filePath))
            {
                MessageBox.Show("Этот файл уже был загружен.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            else
            {
                LoadAndProcessJsonFile(filePath);
            }

        }

        private void LoadAndProcessJsonFile(string filePath)
        {
            // Шаг 1: Чтение содержимого файла
            string fileContent = ReadFileContent(filePath);
            if (fileContent == null) return; // Завершить, если чтение не удалось

            // Шаг 2: Добавление комментариев об ошибках в JSON
            string errorDescription;
            string processedContent = AddErrorCommentsToJson(fileContent, out errorDescription);

            // Шаг 3: Обновление интерфейса после загрузки файла
            UpdateUIAfterFileLoad(processedContent, errorDescription);

            // Шаг 4: Попытка обработать JSON
            if (TryProcessJson(processedContent, out JObject jsonObject))
            {
                // Шаг 5: Обновление дерева представления
                UpdateTreeView(jsonObject);
            }

            // Шаг 6: Обновление списка загруженных файлов
            UpdateLoadedFilesList(filePath);
        }



        private string ReadFileContent(string filePath)
        {
            try
            {
                return File.ReadAllText(filePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при чтении файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }


        private void UpdateUIAfterFileLoad(string processedContent, string errorDescription)
        {

            if (mainWindow == null)
            {
                MessageBox.Show("Ошибка: mainWindow не инициализирован.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            mainWindow.SelectFileTextBlock.Visibility = Visibility.Collapsed;
            mainWindow.StepJsonTextBox.Text = processedContent;

            if (!string.IsNullOrEmpty(errorDescription))
            {
                mainWindow.ErrorJSONTextBox.Text = errorDescription;
                mainWindow.ErrorPanel.Visibility = Visibility.Visible;
            }
            else
            {
                mainWindow.ErrorPanel.Visibility = Visibility.Collapsed;
            }
        }

        private bool TryProcessJson(string processedContent, out JObject jsonObject)
        {
            try
            {
                jsonObject = JObject.Parse(processedContent);
                return true;
            }
            catch (JsonReaderException ex)
            {
                MessageBox.Show($"Ошибка при десериализации JSON: {ex.Message}\n" +
                                $"Строка: {ex.LineNumber}, Позиция: {ex.LinePosition}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            jsonObject = null;
            return false;
        }

        private void UpdateTreeView(JObject jsonObject)
        {
            var treeNodes = treeManager.FormatJsonObject(jsonObject);
            mainWindow.TextTabTreeView.Items.Clear();
            mainWindow.SchemaCanvas.Children.Clear();

            foreach (var node in treeNodes)
            }

            treeManager.ExpandAllTreeViewItems(mainWindow.TextTabTreeView);
            schemaManager.GenerateSchema(jsonObject, mainWindow.SchemaCanvas);
        }

        private void UpdateLoadedFilesList(string filePath)
        {
            mainWindow.LoadedFilesListBox.Items.Add(System.IO.Path.GetFileName(filePath));
            loadedFilePaths.Add(filePath);
            mainWindow.DefaultFileNameTextBlock.Text = filePath;
            UpdateLoadedFilesListVisibility();
        }

        private void UpdateLoadedFilesListVisibility()
        {
            mainWindow.EmptyFilesMessage.Visibility = mainWindow.LoadedFilesListBox.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            mainWindow.LoadedFilesListBox.Visibility = mainWindow.LoadedFilesListBox.Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private string AddErrorCommentsToJson(string jsonContent, out string errorDescription)
        {
            errorDescription = string.Empty;
            var errors = new List<string>();

            if (string.IsNullOrEmpty(jsonContent))
            {
                errorDescription = "JSON контент не может быть пустым.";
                return jsonContent;
            }

            if (!TryParseJson(jsonContent, errors))
            {
                errorDescription = string.Join("\n", errors);
            }

            return jsonContent;
        }

        private bool TryParseJson(string jsonContent, List<string> errors)
        {
            try
            {
                JToken.Parse(jsonContent);
                return true;
            }
            catch (JsonReaderException ex)
            {
                errors.Add($"Ошибка синтаксиса JSON: {ex.Message}\nСтрока: {ex.LineNumber}, Позиция: {ex.LinePosition}");
            }
            return false;
        }

    }

}
