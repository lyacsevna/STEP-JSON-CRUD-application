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

        private MainWindow mainWindow;
        private string currentFilePath;

        public JsonManager(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(MainWindow));
        }

        public void ImportJsonFile()
        {
            var selectedFilePath = OpenFileDialogForJson();
            if (selectedFilePath == null) return;

            if (loadedFilePaths.Contains(selectedFilePath))
            {
                MessageBox.Show("Этот файл уже был загружен.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string fileContent = ReadFileContent(selectedFilePath);
            if (fileContent == null) return;


            ProcessJsonFile(fileContent, selectedFilePath);
            
        }

        private string OpenFileDialogForJson()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON файлы (*.json)|*.json|Все файлы (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() != true)
            {
                MessageBox.Show("Файл не был выбран.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            return openFileDialog.FileName;
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

        public void ProcessJsonFile(string fileContent, string selectedFilePath)
        {
            string errorDescription;
            bool isValidJson = TryValidateJsonSyntax(fileContent, out errorDescription);

            string processedContent = fileContent;

            UpdateInterface(processedContent, isValidJson ? string.Empty : errorDescription, selectedFilePath);

            if (isValidJson && TryConvertJsonToJObject(processedContent, out JObject jsonObject))
            {
                ClearPreviousData();
                FillTreeViewWithJsonNodes(jsonObject);
                GenerateSchema(jsonObject);
                mainWindow.ErrorPanel.Visibility = Visibility.Collapsed;
                currentFilePath = selectedFilePath;
            }
            else
            {
                ShowError(errorDescription);
            }
        }
        public void TestValidCurrentFileContent (string fileContent, string filePat)
        {
            string errorDescription;
            bool isValidJson = TryValidateJsonSyntax(fileContent, out errorDescription);

            string processedContent = fileContent;
            ClearPreviousData();
            if (isValidJson && TryConvertJsonToJObject(processedContent, out JObject jsonObject))
            {
                
                UpdateTreeViewFromJson(processedContent);
            }
            else
            {
                ShowError(errorDescription);
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

        private bool TryValidateJsonSyntax(string jsonContent, out string errorDescription)
        {
            errorDescription = string.Empty;

            if (string.IsNullOrEmpty(jsonContent))
            {
                errorDescription = "JSON контент не может быть пустым.";
                return false;
            }

            try
            {
                JToken.Parse(jsonContent);
                return true;
            }
            catch (JsonReaderException ex)
            {
                errorDescription = $"Ошибка синтаксиса JSON: {ex.Message}\nСтрока: {ex.LineNumber}, Позиция: {ex.LinePosition}";
                return false;
            }
        }

        private void ShowError(string errorDescription)
        {
            mainWindow.ErrorJSONTextBox.Text = errorDescription;
            mainWindow.ErrorPanel.Visibility = Visibility.Visible;
        }



        private void UpdateInterface(string processedContent, string errorDescription, string filePath)
        {
            if (loadedFilePaths.Contains(filePath))
            {
                UpdateTreeViewFromJson(processedContent);
            }
            else
            {
                UpdateUIAfterFileLoad(processedContent, errorDescription, filePath);
            }
        }

        private void UpdateUIAfterFileLoad(string processedContent, string errorDescription, string filePath)
        {
            UpdateLoadedFilesList(filePath);
            ClearPreviousData();
            mainWindow.SelectFileTextBlock.Visibility = Visibility.Collapsed;
            mainWindow.StepJsonTextBox.Text = processedContent;

            if (string.IsNullOrEmpty(errorDescription))
            {
                   
                UpdateTreeViewFromJson(processedContent);
                
            }
            else
            {
               
                ShowError(errorDescription);
            }

            treeManager.ExpandAllTreeViewItems(mainWindow.TextTabTreeView);
        }


        private void UpdateTreeViewFromJson(string processedContent)
        {
            if (!TryConvertJsonToJObject(processedContent, out JObject jsonObject))
            {
                ShowError("Ошибка при преобразовании JSON в объект.");
                return;
            }

            ClearPreviousData();
            FillTreeViewWithJsonNodes(jsonObject);
            GenerateSchema(jsonObject);
            mainWindow.ErrorPanel.Visibility = Visibility.Collapsed;
        }

        private void ClearPreviousData()
        {
            mainWindow.TextTabTreeView.Items.Clear();
            mainWindow.SchemaCanvas.Children.Clear();
        }

        private void FillTreeViewWithJsonNodes(JObject jsonObject)
        {
            var treeNodes = treeManager.FormatJsonObject(jsonObject);
            foreach (var node in treeNodes)
            {
                mainWindow.TextTabTreeView.Items.Add(node);
            }
        }

        private void GenerateSchema(JObject jsonObject)
        {
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

        public void DisplaySelectedFileContent(object sender, SelectionChangedEventArgs e)
        {
            if (mainWindow.LoadedFilesListBox.SelectedItem is string selectedFileName)
            {
                string selectedFilePath = GetSelectedFilePath(selectedFileName);

                if (!string.IsNullOrEmpty(selectedFilePath))
                {
                    string fileContent = ReadFileContent(selectedFilePath);
                    if (fileContent != null)
                    {
                        DisplayFileContent(fileContent, selectedFilePath);
                    }
                    else
                    {
                        MessageBox.Show("Не удалось прочитать содержимое файла.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Пожалуйста, выберите файл из списка.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void DisplayFileContent(string jsonContent, string filePath)
        {
            ClearPreviousData();

            mainWindow.DefaultFileNameTextBlock.Text = filePath;
            mainWindow.StepJsonTextBox.Text = jsonContent;

            string errorDescription;
            bool isValidJson = TryValidateJsonSyntax(jsonContent, out errorDescription);
            string processedContent = jsonContent;

     
            if (isValidJson && TryConvertJsonToJObject(processedContent, out JObject jsonObject))
            {
                
                FillTreeViewWithJsonNodes(jsonObject);
                GenerateSchema(jsonObject);
                mainWindow.ErrorPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                ShowError(errorDescription);
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


        #region Сохранение файла


        public void SaveFile(string action)
        {
            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
            };

            try
            {
                string filePathToSave;

                if (action == "Сохранить")
                {
                    if (string.IsNullOrEmpty(currentFilePath))
                    {
                        // Если текущий путь не установлен, используем диалог "Сохранить как"
                        MessageBox.Show("Сначала используйте 'Сохранить как' для выбора пути.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        if (saveFileDialog.ShowDialog() == true)
                        {
                            filePathToSave = saveFileDialog.FileName;
                            SaveToFile(filePathToSave);
                        }
                    }
                    else
                    {
                        // Если путь установлен, сохраняем файл по этому пути
                        SaveToFile(currentFilePath);
                    }
                }
                else if (action == "Сохранить как")
                {
                    // Используем диалог "Сохранить как"
                    if (saveFileDialog.ShowDialog() == true)
                    {
                        filePathToSave = saveFileDialog.FileName;
                        SaveToFile(filePathToSave);
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
                currentFilePath = filePath; // Обновляем путь после успешного сохранения
                UpdateLoadedFilesList(currentFilePath);
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
