using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace STEP_JSON_Application_for_ASKON
{
    public class JsonManager
    {
        public List<string> loadedFilePaths = new List<string>();
        private static readonly TreeManager treeManager = new TreeManager();
        private MainWindow mainWindow;
        private string currentFilePath;

        public JsonManager(MainWindow mainWindow)
        {
            if (mainWindow == null)
                throw new ArgumentNullException("mainWindow");
            this.mainWindow = mainWindow;
            Console.WriteLine("JsonManager initialized");
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
                MessageBox.Show("Ошибка при чтении файла: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        public void ProcessJsonFile(string fileContent, string selectedFilePath)
        {
            Console.WriteLine($"ProcessJsonFile: filePath={selectedFilePath}, contentLength={fileContent?.Length}");
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

        public void TestValidCurrentFileContent(string fileContent, string filePath)
        {
            Console.WriteLine($"TestValidCurrentFileContent: filePath={filePath}, contentLength={fileContent?.Length}");
            string errorDescription;
            bool isValidJson = TryValidateJsonSyntax(fileContent, out errorDescription);

            string processedContent = fileContent;
            ClearPreviousData();
            if (isValidJson && TryConvertJsonToJObject(processedContent, out JObject jsonObject))
            {
                UpdateTreeViewFromJson(processedContent);
                GenerateSchema(jsonObject);
                mainWindow.ErrorPanel.Visibility = Visibility.Collapsed;
                currentFilePath = filePath;
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
                MessageBox.Show("Ошибка при десериализации JSON: " + ex.Message + "\n" +
                                "Строка: " + ex.LineNumber + ", Позиция: " + ex.LinePosition, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                jsonObject = null;
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                jsonObject = null;
                return false;
            }
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
                errorDescription = "Ошибка синтаксиса JSON: " + ex.Message + "\nСтрока: " + ex.LineNumber + ", Позиция: " + ex.LinePosition;
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
            mainWindow.SchemaManager.GenerateSchema(jsonObject, mainWindow.SchemaCanvas, true);
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
                MessageBox.Show("Ошибка при получении пути к файлу: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return null;
        }

        private string CleanLabel(string label)
        {
            if (string.IsNullOrEmpty(label)) return label;

            // Удаляем "версия ..." и всё после неё
            int versionIndex = label.LastIndexOf("версия", StringComparison.OrdinalIgnoreCase);
            string baseLabel = versionIndex >= 0 ? label.Substring(0, versionIndex).Trim() : label;

            // Разделяем на части
            string[] parts = baseLabel.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return baseLabel;

            // Проверяем, является ли первая часть кодом (например, содержит точки и минимум 3 сегмента)
            string code = parts[0];
            if (code.Contains(".") && code.Split('.').Length >= 3)
            {
                // Собираем оставшиеся части как имя
                string name = string.Join(" ", parts.Skip(1)).Trim();
                // Удаляем code из начала name, если он там есть
                if (name.StartsWith(code, StringComparison.OrdinalIgnoreCase))
                {
                    name = name.Substring(code.Length).Trim();
                }
                return $"{code} {name}".Trim();
            }

            return baseLabel;
        }

        public void UpdateJsonContent(string instanceId, string newLabel, string type, List<JObject> instances)
        {
            Console.WriteLine($"UpdateJsonContent: instanceId={instanceId}, newLabel='{newLabel}', type={type}");
            if (!TryConvertJsonToJObject(mainWindow.StepJsonTextBox.Text, out JObject jsonObject))
            {
                Console.WriteLine("UpdateJsonContent: Failed to parse JSON");
                return;
            }

            var instancesArray = jsonObject["instances"] as JArray;
            if (instancesArray == null)
            {
                Console.WriteLine("UpdateJsonContent: instances array is null");
                return;
            }

            Console.WriteLine($"UpdateJsonContent: instancesArray count={instancesArray.Count}");
            var instance = instancesArray.FirstOrDefault(i => i["id"] != null && i["id"].ToString() == instanceId);
            if (instance == null)
            {
                Console.WriteLine($"UpdateJsonContent: instance not found for id={instanceId}");
                var availableIds = instancesArray.Take(5).Select(i => i["id"]?.ToString() ?? "null").ToList();
                Console.WriteLine($"UpdateJsonContent: Available instance IDs (first 5): {string.Join(", ", availableIds)}");
                return;
            }

            var attributes = instance["attributes"] as JObject;
            if (attributes == null)
            {
                Console.WriteLine($"UpdateJsonContent: attributes is null for id={instanceId}");
                return;
            }

            if (type.Contains("product_definition"))
            {
                var formationId = attributes["formation"] != null ? attributes["formation"].ToString() : null;
                var formation = instancesArray.FirstOrDefault(i => i["id"] != null && i["id"].ToString() == formationId);
                var productId = formation != null && formation["attributes"] != null ? formation["attributes"]["of_product"]?.ToString() : null;
                var product = productId != null ? instancesArray.FirstOrDefault(i => i["id"] != null && i["id"].ToString() == productId) : null;
                if (product != null && product["attributes"] != null)
                {
                    // Очищаем метку от лишних частей
                    string cleanedLabel = CleanLabel(newLabel);
                    // Удаляем defId и productCode из начала метки, если они присутствуют
                    string defId = attributes["id"]?.ToString() ?? "";
                    string productCode = product["attributes"]["id"]?.ToString() ?? "";
                    string name = cleanedLabel;
                    if (!string.IsNullOrEmpty(defId) && name.StartsWith(defId, StringComparison.OrdinalIgnoreCase))
                    {
                        name = name.Substring(defId.Length).Trim();
                    }
                    if (!string.IsNullOrEmpty(productCode) && name.StartsWith(productCode, StringComparison.OrdinalIgnoreCase))
                    {
                        name = name.Substring(productCode.Length).Trim();
                    }
                    Console.WriteLine($"UpdateJsonContent: Setting product.attributes.name='{name}' for productId={productId}");
                    product["attributes"]["name"] = name; // Заменяем только name
                }
                else
                {
                    Console.WriteLine($"UpdateJsonContent: product not found for productId={productId}");
                }
            }
            else if (type == "eskd_product" || type == "organization")
            {
                string cleanedLabel = CleanLabel(newLabel);
                // Для eskd_product удаляем productCode из начала, если он есть
                string productCode = attributes["id"]?.ToString() ?? "";
                string name = cleanedLabel;
                if (!string.IsNullOrEmpty(productCode) && name.StartsWith(productCode, StringComparison.OrdinalIgnoreCase))
                {
                    name = name.Substring(productCode.Length).Trim();
                }
                Console.WriteLine($"UpdateJsonContent: Setting attributes.name='{name}' for instanceId={instanceId}");
                attributes["name"] = name; // Заменяем name
            }

            string updatedJson = JsonConvert.SerializeObject(jsonObject, Formatting.Indented);
            Console.WriteLine("UpdateJsonContent: Updating StepJsonTextBox.Text");
            mainWindow.StepJsonTextBox.Text = updatedJson;

            // Обновляем дерево и схему (только текст)
            Console.WriteLine("UpdateJsonContent: Calling FillTreeViewWithJsonNodes, GenerateSchema");
            FillTreeViewWithJsonNodes(jsonObject);
            mainWindow.SchemaManager.GenerateSchema(jsonObject, mainWindow.SchemaCanvas, false); // Частичное обновление
            mainWindow.ErrorPanel.Visibility = Visibility.Collapsed;
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
                        MessageBox.Show("Сначала используйте 'Сохранить как' для выбора пути.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        if (saveFileDialog.ShowDialog() == true)
                        {
                            filePathToSave = saveFileDialog.FileName;
                            SaveToFile(filePathToSave);
                        }
                    }
                    else
                    {
                        SaveToFile(currentFilePath);
                    }
                }
                else if (action == "Сохранить как")
                {
                    if (saveFileDialog.ShowDialog() == true)
                    {
                        filePathToSave = saveFileDialog.FileName;
                        SaveToFile(filePathToSave);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при сохранении файла: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveToFile(string filePath)
        {
            try
            {
                string contentToSave = GetContentToSave();
                File.WriteAllText(filePath, contentToSave);
                currentFilePath = filePath;
                UpdateLoadedFilesList(currentFilePath);
                MessageBox.Show("Файл успешно сохранен.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при записи файла: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetContentToSave()
        {
            return mainWindow.StepJsonTextBox.Text;
        }
        #endregion
    }
}