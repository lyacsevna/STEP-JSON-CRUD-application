using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows;
using System.Windows.Shapes;

namespace STEP_JSON_Application_for_ASKON
{
    public class SchemaManager
    {
        private readonly double NodeWidth = 200;
        private readonly double NodeHeight = 100;
        private readonly double VerticalSpacing = 100;
        private readonly double HorizontalSpacing = 120;
        private readonly JsonManager jsonManager;
        private string lastJsonContent;
        private bool isUpdating;
        private DateTime lastUpdateTime;

        private class LabelInfo
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Version { get; set; }
        }

        public SchemaManager(JsonManager jsonManager)
        {
            if (jsonManager == null)
                throw new ArgumentNullException("jsonManager");
            this.jsonManager = jsonManager;
            isUpdating = false;
            lastUpdateTime = DateTime.MinValue;
            Console.WriteLine("SchemaManager initialized");
        }

        public void GenerateSchema(JObject jsonObject, Canvas schemaCanvas, bool fullRefresh = true)
        {
            Console.WriteLine($"GenerateSchema called with jsonObject: {(jsonObject != null ? "not null" : "null")}, fullRefresh={fullRefresh}");

            string currentJson = JsonConvert.SerializeObject(jsonObject, Formatting.None);
            if (lastJsonContent == currentJson && !fullRefresh)
            {
                Console.WriteLine("GenerateSchema: Skipping due to identical JSON and partial refresh");
                return;
            }
            lastJsonContent = currentJson;

            TransformGroup transformGroup = null;
            TranslateTransform translateTransform = null;
            ScaleTransform scaleTransform = null;

            if (fullRefresh)
            {
                schemaCanvas.Children.Clear();
                transformGroup = new TransformGroup();
                translateTransform = new TranslateTransform(0, 0);
                scaleTransform = new ScaleTransform(1, 1);
                transformGroup.Children.Add(scaleTransform);
                transformGroup.Children.Add(translateTransform);
                schemaCanvas.RenderTransform = transformGroup;
            }

            if (jsonObject == null || jsonObject["instances"] == null)
            {
                MessageBox.Show("В JSON отсутствует массив 'instances'.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                Console.WriteLine("GenerateSchema: JSON or instances is null");
                return;
            }

            var instances = jsonObject["instances"].ToObject<List<JObject>>();
            if (instances == null || instances.Count == 0)
            {
                MessageBox.Show("Массив 'instances' пуст или некорректен.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                Console.WriteLine("GenerateSchema: instances is null or empty");
                return;
            }

            var nodes = new Dictionary<string, (UIElement Element, double X, double Y)>();
            var relationships = new List<(string ParentId, string ChildId, string Label, string Type)>();
            var processedRelationships = new HashSet<string>();

            foreach (var instance in instances)
            {
                string id = instance["id"]?.ToString();
                if (string.IsNullOrEmpty(id))
                {
                    Console.WriteLine("GenerateSchema: Skipping instance with null or empty id");
                    continue;
                }

                string type = instance["type"]?.ToString() ?? "unknown";
                var attributes = instance["attributes"] as JObject;

                if (attributes != null)
                {
                    if (type.Contains("next_assembly_usage_occurrence"))
                    {
                        string relatingId = attributes["relating_product_definition"]?.ToString();
                        string relatedId = attributes["related_product_definition"]?.ToString();
                        string relId = attributes["id"]?.ToString();
                        if (!string.IsNullOrEmpty(relatingId) && !string.IsNullOrEmpty(relatedId) && !string.IsNullOrEmpty(relId) && !processedRelationships.Contains(relId))
                        {
                            string refDesignator = attributes["reference_designator"]?.ToString();
                            string quantityId = attributes["quantity"]?.ToString();
                            string quantityLabel = GetQuantityLabel(quantityId, instances);
                            string unit = GetUnitForQuantity(quantityId, instances);

                            string label = string.IsNullOrEmpty(refDesignator) && string.IsNullOrEmpty(quantityLabel)
                                ? "Состоит из,\nкол-во неизвестно"
                                : string.IsNullOrEmpty(refDesignator)
                                    ? $"Состоит из,\nкол-во {quantityLabel} {unit}"
                                    : $"Состоит из,\nпоз.{refDesignator},\nкол-во {quantityLabel} {unit}";

                            relationships.Add((relatingId, relatedId, label, "composition"));
                            processedRelationships.Add(relId);
                        }
                    }
                    else if (type == "eskd_organization_product_assignment")
                    {
                        string productId = attributes["assigned_product"]?.ToString();
                        string orgId = attributes["assigned_organization"]?.ToString();
                        string relId = attributes["id"]?.ToString();
                        if (!string.IsNullOrEmpty(productId) && !string.IsNullOrEmpty(orgId) && !string.IsNullOrEmpty(relId) && !processedRelationships.Contains(relId))
                        {
                            string roleId = attributes["role"]?.ToString();
                            string role = instances.FirstOrDefault(i => i["id"]?.ToString() == roleId)?["attributes"]?["name"]?.ToString() ?? "Назначена организация";
                            relationships.Add((productId, orgId, role, "organization"));
                            processedRelationships.Add(relId);
                        }
                    }
                    else if (type == "product_definition")
                    {
                        string defId = instance["id"]?.ToString();
                        string formationId = attributes["formation"]?.ToString();
                        if (!string.IsNullOrEmpty(defId) && !string.IsNullOrEmpty(formationId))
                        {
                            string relId = $"{defId}-{formationId}";
                            if (!processedRelationships.Contains(relId))
                            {
                                relationships.Add((defId, formationId, "Версия", "version"));
                                processedRelationships.Add(relId);
                            }
                        }
                    }
                }
            }

            var allIds = instances.Select(i => i["id"]?.ToString()).Where(id => !string.IsNullOrEmpty(id)).ToHashSet();
            var childIds = relationships.Select(r => r.ChildId).ToHashSet();
            var rootIds = allIds.Except(childIds).ToList();

            var levels = new Dictionary<int, List<(string Id, JObject Instance)>>();
            var usedIds = new HashSet<string>();

            void BuildTree(string nodeId, int nodeLevel)
            {
                if (string.IsNullOrEmpty(nodeId) || !relationships.Any(r => r.ParentId == nodeId))
                    return;

                if (!levels.ContainsKey(nodeLevel))
                    levels[nodeLevel] = new List<(string, JObject)>();

                var nodeInstance = instances.FirstOrDefault(i => i["id"]?.ToString() == nodeId);
                if (nodeInstance != null && !usedIds.Contains(nodeId))
                {
                    levels[nodeLevel].Add((nodeId, nodeInstance));
                    usedIds.Add(nodeId);
                    foreach (var childId in relationships.Where(r => r.ParentId == nodeId).Select(r => r.ChildId))
                    {
                        BuildTree(childId, nodeLevel + 1);
                    }
                }
            }

            foreach (var rootId in rootIds)
            {
                BuildTree(rootId, 0);
            }

            if (fullRefresh)
            {
                double maxCanvasWidth = 0;
                double maxCanvasHeight = 0;

                foreach (var levelEntry in levels.OrderBy(l => l.Key))
                {
                    double levelY = levelEntry.Key * (NodeHeight + VerticalSpacing) + 20;
                    int nodesInLevel = levelEntry.Value.Count;
                    double totalWidth = nodesInLevel * NodeWidth + (nodesInLevel - 1) * HorizontalSpacing;
                    double startX = (schemaCanvas.Width - totalWidth) / 2;
                    if (startX < 20) startX = 20;

                    int nodeIndex = 0;
                    foreach (var (nodeId, nodeInstance) in levelEntry.Value)
                    {
                        double levelX = startX + nodeIndex * (NodeWidth + HorizontalSpacing);
                        string type = nodeInstance["type"]?.ToString() ?? "unknown";
                        var labelInfo = GetFriendlyLabel(nodeId, nodeInstance, instances);

                        var node = CreateStyledEllipse(nodeId, type, labelInfo, instances);
                        Canvas.SetLeft(node, levelX);
                        Canvas.SetTop(node, levelY);
                        schemaCanvas.Children.Add(node);
                        nodes[nodeId] = (node, levelX, levelY);

                        nodeIndex++;
                        maxCanvasWidth = Math.Max(maxCanvasWidth, levelX + NodeWidth);
                    }
                    maxCanvasHeight = Math.Max(maxCanvasHeight, levelY + NodeHeight);
                }

                schemaCanvas.Width = Math.Max(schemaCanvas.Width, maxCanvasWidth + 20);
                schemaCanvas.Height = Math.Max(maxCanvasHeight + 20, schemaCanvas.Height);

                foreach (var rel in relationships)
                {
                    string parentId = rel.ParentId;
                    string childId = rel.ChildId;
                    string label = rel.Label;
                    string relType = rel.Type;

                    if (nodes.ContainsKey(parentId) && nodes.ContainsKey(childId))
                    {
                        var parent = nodes[parentId];
                        var child = nodes[childId];

                        double startX = parent.X + NodeWidth / 2;
                        double startY = parent.Y + NodeHeight;
                        double endX = child.X + NodeWidth / 2;
                        double endY = child.Y;

                        var connection = CreateSimpleConnection(relType, startX, startY, endX, endY);
                        schemaCanvas.Children.Add(connection.Line);
                        schemaCanvas.Children.Add(connection.Arrow);

                        var labelText = new TextBlock
                        {
                            Text = label,
                            FontSize = 12,
                            Foreground = Brushes.Black,
                            Background = Brushes.White,
                            Padding = new Thickness(2),
                            TextAlignment = TextAlignment.Center,
                            TextWrapping = TextWrapping.Wrap,
                            MaxWidth = 120
                        };
                        labelText.Measure(new Size(120, double.PositiveInfinity));
                        double midX = startX + (endX - startX) / 2;
                        double midY = startY + (endY - startY) / 2;
                        Canvas.SetLeft(labelText, midX - labelText.DesiredSize.Width / 2);
                        Canvas.SetTop(labelText, midY - labelText.DesiredSize.Height / 2);
                        schemaCanvas.Children.Add(labelText);
                    }
                }

                Point lastMousePosition = new Point();
                bool isDragging = false;

                schemaCanvas.MouseLeftButtonDown += (sender, e) =>
                {
                    schemaCanvas.Cursor = Cursors.Hand;
                    lastMousePosition = e.GetPosition(schemaCanvas);
                    isDragging = true;
                    schemaCanvas.CaptureMouse();
                    CompositionTarget.Rendering += UpdateCanvasPosition;
                };

                void UpdateCanvasPosition(object sender, EventArgs e)
                {
                    if (isDragging)
                    {
                        Point currentPosition = Mouse.GetPosition(schemaCanvas);
                        Vector delta = currentPosition - lastMousePosition;
                        lastMousePosition = currentPosition;
                        translateTransform.X += delta.X;
                        translateTransform.Y += delta.Y;
                    }
                }

                schemaCanvas.MouseLeftButtonUp += (sender, e) =>
                {
                    schemaCanvas.Cursor = Cursors.Arrow;
                    isDragging = false;
                    schemaCanvas.ReleaseMouseCapture();
                    CompositionTarget.Rendering -= UpdateCanvasPosition;
                };

                schemaCanvas.MouseLeave += (sender, e) =>
                {
                    if (isDragging)
                    {
                        schemaCanvas.Cursor = Cursors.Arrow;
                        isDragging = false;
                        schemaCanvas.ReleaseMouseCapture();
                        CompositionTarget.Rendering -= UpdateCanvasPosition;
                    }
                };

                schemaCanvas.MouseWheel += (sender, e) =>
                {
                    if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                    {
                        double scaleFactor = e.Delta > 0 ? 1.1 : 0.9;
                        double newScaleX = scaleTransform.ScaleX * scaleFactor;
                        double newScaleY = scaleTransform.ScaleY * scaleFactor;
                        double viewWidth = schemaCanvas.ActualWidth;
                        double viewHeight = schemaCanvas.ActualHeight;
                        double minScaleX = Math.Max(viewWidth / schemaCanvas.Width, 0.5);
                        double minScaleY = Math.Max(viewHeight / schemaCanvas.Height, 0.5);
                        double minScale = Math.Max(minScaleX, minScaleY);

                        if (newScaleX >= minScale && newScaleX <= 5)
                        {
                            Point mousePosition = e.GetPosition(schemaCanvas);
                            scaleTransform.CenterX = mousePosition.X;
                            scaleTransform.CenterY = mousePosition.Y;
                            scaleTransform.ScaleX = newScaleX;
                            scaleTransform.ScaleY = newScaleY;
                        }
                        e.Handled = true;
                    }
                };
            }
            else
            {
                foreach (var levelEntry in levels.OrderBy(l => l.Key))
                {
                    foreach (var (nodeId, nodeInstance) in levelEntry.Value)
                    {
                        string type = nodeInstance["type"]?.ToString() ?? "unknown";
                        var labelInfo = GetFriendlyLabel(nodeId, nodeInstance, instances);
                        UpdateEllipseLabel(nodeId, labelInfo, schemaCanvas);
                    }
                }
            }
        }

        private string GetQuantityLabel(string quantityId, List<JObject> instances)
        {
            if (string.IsNullOrEmpty(quantityId))
                return "";

            var quantity = instances.FirstOrDefault(i => i["id"]?.ToString() == quantityId && i["type"]?.ToString() == "measure_with_unit");
            if (quantity == null)
            {
                Console.WriteLine($"GetQuantityLabel: Quantity not found for id={quantityId}");
                return "";
            }

            string value = quantity["attributes"]?["value_component"]?.ToString() ?? "";
            return value.Trim();
        }

        private string GetUnitForQuantity(string quantityId, List<JObject> instances)
        {
            if (string.IsNullOrEmpty(quantityId))
                return "шт.";

            var quantity = instances.FirstOrDefault(i => i["id"]?.ToString() == quantityId && i["type"]?.ToString() == "measure_with_unit");
            if (quantity == null)
            {
                Console.WriteLine($"GetUnitForQuantity: Quantity not found for id={quantityId}");
                return "шт.";
            }

            string unitId = quantity["attributes"]?["unit_component"]?.ToString();
            var unit = unitId != null ? instances.FirstOrDefault(i => i["id"]?.ToString() == unitId && i["type"]?.ToString() == "context_dependent_unit") : null;
            string unitName = unit?["attributes"]?["id"]?.ToString() ?? "шт.";
            return unitName;
        }

        private LabelInfo GetFriendlyLabel(string id, JObject instance, List<JObject> instances)
        {
            string type = instance["type"]?.ToString() ?? "unknown";
            string defId = "Неизвестно";
            string name = "";
            string version = "";

            if (type.Contains("product_definition"))
            {
                defId = instance["attributes"]?["id"]?.ToString() ?? "Unknown Definition";
                string formationId = instance["attributes"]?["formation"]?.ToString();

                if (!string.IsNullOrEmpty(formationId))
                {
                    var formation = instances.FirstOrDefault(i => i["id"]?.ToString() == formationId);
                    string productId = formation?["attributes"]?["of_product"]?.ToString();
                    version = formation?["attributes"]?["id"]?.ToString() ?? "";

                    if (!string.IsNullOrEmpty(productId))
                    {
                        var product = instances.FirstOrDefault(i => i["id"]?.ToString() == productId);
                        defId = product?["attributes"]?["id"]?.ToString() ?? defId;
                        name = product?["attributes"]?["name"]?.ToString() ?? "";
                    }
                }
            }
            else if (type == "eskd_product")
            {
                defId = instance["attributes"]?["id"]?.ToString() ?? "Unknown Product";
                name = instance["attributes"]?["name"]?.ToString() ?? "";
            }
            else if (type == "organization")
            {
                defId = instance["attributes"]?["id"]?.ToString() ?? "Unknown Organization";
                name = instance["attributes"]?["name"]?.ToString() ?? "Организация";
            }

            return new LabelInfo { Id = defId, Name = name, Version = version };
        }

        private string CleanLabel(string label)
        {
            if (string.IsNullOrEmpty(label))
                return label;

            string cleaned = label.Trim();
            if (cleaned.StartsWith("id:", StringComparison.OrdinalIgnoreCase))
                cleaned = cleaned.Substring(3).Trim();
            else if (cleaned.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
                cleaned = cleaned.Substring(5).Trim();
            else if (cleaned.StartsWith("версия:", StringComparison.OrdinalIgnoreCase))
                cleaned = cleaned.Substring(7).Trim();

            cleaned = cleaned.Replace("\"", "\\\"").Replace("\\", "\\\\");
            return cleaned;
        }

        private UIElement CreateStyledEllipse(string id, string type, LabelInfo labelInfo, List<JObject> instances)
        {
            Console.WriteLine($"CreateStyledEllipse: id={id}, type={type}, labelInfo=({labelInfo.Id}, {labelInfo.Name}, {labelInfo.Version})");

            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = NodeWidth - 20,
                Margin = new Thickness(2)
            };

            var idTextBox = new TextBox
            {
                Text = labelInfo.Id,
                FontSize = 12,
                Foreground = Brushes.Black,
                Background = null,
                BorderBrush = null,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Left,
                Tag = "id:" + id
            };
            var idLabel = new TextBlock
            {
                Text = "id:",
                FontSize = 12,
                Foreground = new SolidColorBrush(Colors.Gray) { Opacity = 0.5 },
                IsHitTestVisible = false,
                Margin = new Thickness(0, 0, 2, 0),
                Visibility = Visibility.Collapsed
            };

            var nameTextBox = new TextBox
            {
                Text = labelInfo.Name,
                FontSize = 12,
                Foreground = Brushes.Black,
                Background = null,
                BorderBrush = null,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Left,
                Tag = "name:" + id
            };
            var nameLabel = new TextBlock
            {
                Text = "name:",
                FontSize = 12,
                Foreground = new SolidColorBrush(Colors.Gray) { Opacity = 0.5 },
                IsHitTestVisible = false,
                Margin = new Thickness(0, 0, 2, 0),
                Visibility = Visibility.Collapsed
            };

            var versionTextBox = new TextBox
            {
                Text = labelInfo.Version,
                FontSize = 12,
                Foreground = Brushes.Black,
                Background = null,
                BorderBrush = null,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Left,
                Tag = "version:" + id
            };
            var versionLabel = new TextBlock
            {
                Text = "version:",
                FontSize = 12,
                Foreground = new SolidColorBrush(Colors.Gray) { Opacity = 0.5 },
                IsHitTestVisible = false,
                Margin = new Thickness(0, 0, 2, 0),
                Visibility = Visibility.Collapsed
            };

            double fontSize = 12;
            stackPanel.Children.Add(CreateLabelPanel(idLabel, idTextBox));
            stackPanel.Children.Add(CreateLabelPanel(nameLabel, nameTextBox));
            stackPanel.Children.Add(CreateLabelPanel(versionLabel, versionTextBox));

            stackPanel.Measure(new Size(NodeWidth - 20, NodeHeight - 20));
            while (stackPanel.DesiredSize.Height > NodeHeight - 20 && fontSize > 8)
            {
                fontSize -= 0.5;
                idTextBox.FontSize = fontSize;
                nameTextBox.FontSize = fontSize;
                versionTextBox.FontSize = fontSize;
                idLabel.FontSize = fontSize;
                nameLabel.FontSize = fontSize;
                versionLabel.FontSize = fontSize;
                stackPanel.Measure(new Size(NodeWidth - 20, NodeHeight - 20));
            }

            void AddTextBoxHandlers(TextBox textBox, TextBlock label, string field)
            {
                textBox.GotFocus += (sender, e) =>
                {
                    label.Visibility = Visibility.Visible;
                    Console.WriteLine($"GotFocus: id={id}, field={field}, currentText={textBox.Text}");
                };

                textBox.LostFocus += (sender, e) =>
                {
                    label.Visibility = Visibility.Collapsed;
                    if (isUpdating || (DateTime.Now - lastUpdateTime).TotalMilliseconds < 500)
                        return;

                    isUpdating = true;
                    lastUpdateTime = DateTime.Now;
                    try
                    {
                        string cleanedText = CleanLabel(textBox.Text);
                        var currentLabel = GetFriendlyLabel(id, instances.FirstOrDefault(i => i["id"]?.ToString() == id), instances);
                        string currentValue = field == "id" ? currentLabel.Id : field == "name" ? currentLabel.Name : currentLabel.Version;

                        if (cleanedText != currentValue)
                        {
                            Console.WriteLine($"LostFocus: id={id}, field={field}, newValue='{cleanedText}', currentValue='{currentValue}'");
                            jsonManager.UpdateJsonContent(id, field, cleanedText, instances);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"LostFocus: Error updating JSON for id={id}, field={field}: {ex.Message}");
                    }
                    finally
                    {
                        isUpdating = false;
                    }
                };

                textBox.KeyDown += (sender, e) =>
                {
                    if (e.Key == Key.Enter || e.Key == Key.Return)
                    {
                        label.Visibility = Visibility.Collapsed;
                        if (isUpdating || (DateTime.Now - lastUpdateTime).TotalMilliseconds < 500)
                            return;

                        isUpdating = true;
                        lastUpdateTime = DateTime.Now;
                        try
                        {
                            string cleanedText = CleanLabel(textBox.Text);
                            var currentLabel = GetFriendlyLabel(id, instances.FirstOrDefault(i => i["id"]?.ToString() == id), instances);
                            string currentValue = field == "id" ? currentLabel.Id : field == "name" ? currentLabel.Name : currentLabel.Version;

                            if (cleanedText != currentValue)
                            {
                                Console.WriteLine($"KeyDown: id={id}, field={field}, newValue='{cleanedText}', currentValue='{currentValue}'");
                                jsonManager.UpdateJsonContent(id, field, cleanedText, instances);
                                Keyboard.ClearFocus();
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"KeyDown: Error updating JSON for id={id}, field={field}: {ex.Message}");
                        }
                        finally
                        {
                            isUpdating = false;
                        }
                    }
                };
            }

            AddTextBoxHandlers(idTextBox, idLabel, "id");
            AddTextBoxHandlers(nameTextBox, nameLabel, "name");
            AddTextBoxHandlers(versionTextBox, versionLabel, "version");

            var ellipse = new Ellipse
            {
                Width = NodeWidth,
                Height = NodeHeight,
                Fill = Brushes.Transparent,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };

            var container = new Canvas
            {
                Width = NodeWidth,
                Height = NodeHeight,
                Tag = id
            };
            container.Children.Add(ellipse);
            container.Children.Add(stackPanel);

            Canvas.SetLeft(stackPanel, (NodeWidth - stackPanel.DesiredSize.Width) / 2);
            Canvas.SetTop(stackPanel, (NodeHeight - stackPanel.DesiredSize.Height) / 2);

            return container;
        }

        private StackPanel CreateLabelPanel(TextBlock label, TextBox textBox)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(2)
            };
            panel.Children.Add(label);
            panel.Children.Add(textBox);
            return panel;
        }

        private void UpdateEllipseLabel(string id, LabelInfo labelInfo, Canvas schemaCanvas)
        {
            Console.WriteLine($"UpdateEllipseLabel: id={id}, labelInfo=({labelInfo?.Id}, {labelInfo?.Name}, {labelInfo?.Version})");
            var container = schemaCanvas.Children.OfType<Canvas>().FirstOrDefault(c => c.Tag?.ToString() == id);
            if (container != null)
            {
                var stackPanel = container.Children.OfType<StackPanel>().FirstOrDefault();
                if (stackPanel != null)
                {
                    var textBoxes = stackPanel.Children.OfType<StackPanel>()
                        .SelectMany(p => p.Children.OfType<TextBox>())
                        .ToList();

                    var idTextBox = textBoxes.FirstOrDefault(tb => tb.Tag.ToString().StartsWith("id:"));
                    var nameTextBox = textBoxes.FirstOrDefault(tb => tb.Tag.ToString().StartsWith("name:"));
                    var versionTextBox = textBoxes.FirstOrDefault(tb => tb.Tag.ToString().StartsWith("version:"));

                    if (idTextBox != null && labelInfo.Id != null)
                    {
                        idTextBox.Text = labelInfo.Id;
                        Console.WriteLine($"UpdateEllipseLabel: idTextBox updated to '{labelInfo.Id}'");
                    }
                    if (nameTextBox != null && labelInfo.Name != null)
                    {
                        nameTextBox.Text = labelInfo.Name;
                        Console.WriteLine($"UpdateEllipseLabel: nameTextBox updated to '{labelInfo.Name}'");
                    }
                    if (versionTextBox != null)
                    {
                        versionTextBox.Text = labelInfo?.Version ?? "";
                        versionTextBox.Visibility = string.IsNullOrEmpty(labelInfo?.Version) ? Visibility.Collapsed : Visibility.Visible;
                        Console.WriteLine($"UpdateEllipseLabel: versionTextBox updated to '{labelInfo?.Version}'");
                    }

                    double fontSize = 12;
                    stackPanel.Measure(new Size(NodeWidth - 20, NodeHeight - 20));
                    while (stackPanel.DesiredSize.Height > NodeHeight - 20 && fontSize > 8)
                    {
                        fontSize -= 0.5;
                        if (idTextBox != null) idTextBox.FontSize = fontSize;
                        if (nameTextBox != null) nameTextBox.FontSize = fontSize;
                        if (versionTextBox != null) versionTextBox.FontSize = fontSize;
                        stackPanel.Children.OfType<StackPanel>().ToList().ForEach(p =>
                        {
                            var label = p.Children.OfType<TextBlock>().FirstOrDefault();
                            if (label != null) label.FontSize = fontSize;
                        });
                        stackPanel.Measure(new Size(NodeWidth - 20, NodeHeight - 20));
                    }

                    Canvas.SetLeft(stackPanel, (NodeWidth - stackPanel.DesiredSize.Width) / 2);
                    Canvas.SetTop(stackPanel, (NodeHeight - stackPanel.DesiredSize.Height) / 2);
                }
            }
        }

        private (Line Line, Polygon Arrow) CreateSimpleConnection(string relType, double startX, double startY, double endX, double endY)
        {
            var line = new Line
            {
                X1 = startX,
                Y1 = startY,
                X2 = endX,
                Y2 = endY,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };

            if (relType == "organization")
            {
                line.StrokeDashArray = new DoubleCollection { 4, 2 };
            }

            var arrow = new Polygon
            {
                Points = new PointCollection
                {
                    new Point(0, 0),
                    new Point(-8, 4),
                    new Point(-8, -4)
                },
                Fill = Brushes.Black
            };
            double angle = Math.Atan2(endY - startY, endX - startX) * 180 / Math.PI;
            arrow.RenderTransform = new RotateTransform(angle, 0, 0);
            Canvas.SetLeft(arrow, endX);
            Canvas.SetTop(arrow, endY);

            return (line, arrow);
        }
    }
}