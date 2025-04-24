using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Windows;
using System.Windows.Controls;

namespace STEP_JSON_Application_for_ASKON
{
    public class JsonManager
    {


        public List<string> loadedFilePaths = new List<string>();
        
        private static readonly TreeManager treeManager = new TreeManager();
        private static readonly SchemaManager schemaManager = new SchemaManager();
        internal readonly object loadedFilePathss;
        private MainWindow mainWindow;
        private string currentFilePath;

        public JsonManager(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(MainWindow));
        }

        #region Импорт файла
        public void OpenAndLoadUniqueJsonFile()
        {
            
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON файлы (*.json)|*.json|Все файлы (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() != true || string.IsNullOrEmpty(openFileDialog.FileName))
            {
                MessageBox.Show("Файл не был выбран.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
           
            string selectedFilePath = openFileDialog.FileName;

            if (loadedFilePaths.Contains(selectedFilePath))
            {
                MessageBox.Show("Этот файл уже был загружен.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LoadAndProcessJsonFile(selectedFilePath);
        }
        public void LoadAndProcessJsonFile(string filePath)
        {
            string fileContent = ReadFileContent(filePath);
            if (fileContent == null) return;

            // Обработка JSON файла, даже если есть ошибки
            string errorDescription = string.Empty;
            string processedContent = AddErrorCommentsToJson(fileContent, out errorDescription);

            // Обновляем интерфейс, даже если есть ошибки
            UpdateUIAfterFileLoad(processedContent, errorDescription);

            // Обновляем дерево и список загруженных файлов
            //UpdateTreeView(processedContent); // Передаем обработанный контент
            UpdateLoadedFilesList(filePath);

            // Устанавливаем currentFilePath после загрузки файла
            currentFilePath = filePath;
        }

        #region Методы, вызываемые LoadAndProcessJsonFile
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

        public void ProcessJsonFile(string jsonContent)
        {
            
            string errorDescription;
            string processedContent = AddErrorCommentsToJson(jsonContent, out errorDescription);

            if (TryConvertJsonToJObject(processedContent, out JObject jsonObject))
            {
                UpdateUIAfterFileLoad(processedContent, errorDescription);
                UpdateTreeView(jsonObject);

            }
        }

        public string AddErrorCommentsToJson(string jsonContent, out string errorDescription)
        {
            errorDescription = string.Empty;
            var errors = new List<string>();

            if (string.IsNullOrEmpty(jsonContent))
            {
                errorDescription = "JSON контент не может быть пустым.";
                return jsonContent;
            }

            if (!TryValidateJsonSyntax(jsonContent, errors))
            {
                errorDescription = string.Join("\n", errors);
            }

            return jsonContent;
        }

        private void UpdateUIAfterFileLoad(string processedContent, string errorDescription)
        {

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

        private bool TryConvertJsonToJObject(string processedContent, out JObject jsonObject)
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

        private bool TryValidateJsonSyntax(string jsonContent, List<string> errors)
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

        private void UpdateTreeView(JObject jsonObject)
        {
            var treeNodes = treeManager.FormatJsonObject(jsonObject);
            mainWindow.TextTabTreeView.Items.Clear();
            mainWindow.SchemaCanvas.Children.Clear();

            foreach (var node in treeNodes)
            {
                mainWindow.TextTabTreeView.Items.Add(node);
            }

            treeManager.ExpandAllTreeViewItems(mainWindow.TextTabTreeView);
            schemaManager.GenerateSchema(jsonObject, mainWindow.SchemaCanvas);
        }

        private void UpdateLoadedFilesList(string filePath)
        {
            mainWindow.LoadedFilesListBox.Items.Add(System.IO.Path.GetFileName(filePath));
            loadedFilePaths.Add(filePath);
            mainWindow.DefaultFileNameTextBlock.Text = filePath;

            bool hasFiles = mainWindow.LoadedFilesListBox.Items.Count > 0;
            mainWindow.EmptyFilesMessage.Visibility = hasFiles ? Visibility.Collapsed : Visibility.Visible;
            mainWindow.LoadedFilesListBox.Visibility = hasFiles ? Visibility.Visible : Visibility.Collapsed;
        }


        public void LoadedFilesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (mainWindow.LoadedFilesListBox.SelectedItem is string selectedFileName)
            {
                string selectedFilePath = GetSelectedFilePath(selectedFileName);

                
                if (!string.IsNullOrEmpty(selectedFilePath))
                {
                    try
                    {
                        LoadAndProcessJsonFile(selectedFilePath);
                    }
                    catch (Exception ex)
                    {
                        
                        MessageBox.Show($"Ошибка при загрузке файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private string GetSelectedFilePath(string selectedFileName)
        {
            try
            {
                int index = mainWindow.LoadedFilesListBox.Items.IndexOf(selectedFileName);
                if (index >= 0 && index < loadedFilePaths.Count)
                {
                    return loadedFilePaths[index];
                }
                else
                {
                    MessageBox.Show("Выбранный файл не найден в списке загруженных файлов.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                
                MessageBox.Show($"Ошибка при получении пути к файлу: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return null;
        }



        #endregion

        #endregion


        #region Сохранение файла


        public void SaveFile(string action)
        {
            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
            };

            try
            {
                if (action == "Сохранить")
                {
                    if (!string.IsNullOrEmpty(currentFilePath))
                    {
                        SaveToFile(currentFilePath);
                    }
                    else
                    {
                        MessageBox.Show("Сначала используйте 'Сохранить как' для выбора пути.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        if (saveFileDialog.ShowDialog() == true)
                        {
                            SaveToFile(saveFileDialog.FileName);
                            currentFilePath = saveFileDialog.FileName;
                            UpdateLoadedFilesList(currentFilePath); 
                        }
                    }
                }
                else if (action == "Сохранить как")
                {
                    if (saveFileDialog.ShowDialog() == true)
                    {
                        SaveToFile(saveFileDialog.FileName);
                        currentFilePath = saveFileDialog.FileName;
                        UpdateLoadedFilesList(currentFilePath);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveToFile(string filePath)
        {
            try
            {
                string contentToSave = GetContentToSave();
                File.WriteAllText(filePath, contentToSave);
                MessageBox.Show("Файл успешно сохранен.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при записи файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetContentToSave()
        {
            return mainWindow.StepJsonTextBox.Text; 
        }

   


        #endregion
    }

}
