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

        public void GenerateSchema(JObject jsonObject, Canvas schemaCanvas)
        {
            // Очищаем холст перед генерацией новой схемы
            schemaCanvas.Children.Clear();

            // Создаем группу трансформаций для перемещения и масштабирования
            TransformGroup transformGroup = new TransformGroup();
            TranslateTransform translateTransform = new TranslateTransform(0, 0);
            ScaleTransform scaleTransform = new ScaleTransform(1, 1);
            transformGroup.Children.Add(scaleTransform);
            transformGroup.Children.Add(translateTransform);
            schemaCanvas.RenderTransform = transformGroup;

            if (jsonObject == null || jsonObject["instances"] == null)
            {
                MessageBox.Show("В JSON отсутствует массив 'instances'.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var instances = jsonObject["instances"].ToObject<List<JObject>>();
            if (instances == null || instances.Count == 0)
            {
                MessageBox.Show("Массив 'instances' пуст или некорректен.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var nodes = new Dictionary<string, (UIElement Element, double X, double Y)>();
            var relationships = new List<(string ParentId, string ChildId, string Label, string Type)>();

            foreach (var instance in instances)
            {
                string type = instance["type"] != null ? instance["type"].ToString() : "unknown";
                var attributes = instance["attributes"] as JObject;

                if (attributes != null)
                {
                    if (type.Contains("next_assembly_usage_occurrence"))
                    {
                        string relatingId = attributes["relating_product_definition"]?.ToString();
                        string relatedId = attributes["related_product_definition"]?.ToString();
                        if (!string.IsNullOrEmpty(relatingId) && !string.IsNullOrEmpty(relatedId))
                        {
                            string refDesignator = attributes["reference_designator"]?.ToString();
                            string quantityId = attributes["quantity"]?.ToString();
                            string quantityLabel = GetQuantityLabel(quantityId, instances);
                            string unit = GetUnitForQuantity(quantityId, instances);

                            string label;
                            if (string.IsNullOrEmpty(refDesignator) && string.IsNullOrEmpty(quantityLabel))
                            {
                                label = "Состоит из,\nкол-во неизвестно";
                            }
                            else if (string.IsNullOrEmpty(refDesignator))
                            {
                                label = $"Состоит из,\nкол-во {quantityLabel} {unit}";
                            }
                            else
                            {
                                label = $"Состоит из,\nпоз.{refDesignator},\nкол-во {quantityLabel} {unit}";
                            }
                            relationships.Add((relatingId, relatedId, label, "composition"));
                        }
                    }
                    else if (type == "eskd_organization_product_assignment")
                    {
                        string productId = attributes["assigned_product"]?.ToString();
                        string orgId = attributes["assigned_organization"]?.ToString();
                        if (!string.IsNullOrEmpty(productId) && !string.IsNullOrEmpty(orgId))
                        {
                            string roleId = attributes["role"]?.ToString();
                            string role = instances.FirstOrDefault(i => i["id"]?.ToString() == roleId)?["attributes"]?["name"]?.ToString() ?? "Назначена организация";
                            relationships.Add((productId, orgId, role, "organization"));
                        }
                    }
                    else if (type == "product_definition")
                    {
                        string defId = instance["id"]?.ToString();
                        string formationId = attributes["formation"]?.ToString();
                        if (!string.IsNullOrEmpty(defId) && !string.IsNullOrEmpty(formationId))
                        {
                            relationships.Add((defId, formationId, "Версия", "version"));
                        }
                    }
                }
            }

            var allIds = instances.Select(i => i["id"]?.ToString()).Where(id => !string.IsNullOrEmpty(id)).ToHashSet();
            var childIds = relationships.Select(r => r.ChildId).ToHashSet();
            var rootIds = allIds.Except(childIds).ToList();

            var levels = new Dictionary<int, List<(string Id, JObject Instance)>>();
            var usedIds = new HashSet<string>();

            void BuildTree(string id, int level)
            {
                if (!relationships.Any(r => r.ParentId == id)) return;

                if (!levels.ContainsKey(level))
                    levels[level] = new List<(string, JObject)>();

                var instance = instances.FirstOrDefault(i => i["id"]?.ToString() == id);
                if (instance != null && !levels[level].Any(l => l.Id == id))
                {
                    levels[level].Add((id, instance));
                    usedIds.Add(id);
                    foreach (var childId in relationships.Where(r => r.ParentId == id).Select(r => r.ChildId))
                    {
                        BuildTree(childId, level + 1);
                    }
                }
            }

            foreach (var rootId in rootIds)
            {
                BuildTree(rootId, 0);
            }

            double maxCanvasWidth = 0;
            double maxCanvasHeight = 0;

            foreach (var level in levels.OrderBy(l => l.Key))
            {
                double levelY = level.Key * (NodeHeight + VerticalSpacing) + 20;
                int nodesInLevel = level.Value.Count;
                double totalWidth = nodesInLevel * NodeWidth + (nodesInLevel - 1) * HorizontalSpacing;
                double startX = (schemaCanvas.Width - totalWidth) / 2;
                if (startX < 20) startX = 20;

                int nodeIndex = 0;
                foreach (var (id, instance) in level.Value)
                {
                    double levelX = startX + nodeIndex * (NodeWidth + HorizontalSpacing);
                    string type = instance["type"]?.ToString() ?? "unknown";
                    string label = GetFriendlyLabel(id, instance, instances);

                    var node = CreateStyledEllipse(type, label);
                    Canvas.SetLeft(node, levelX);
                    Canvas.SetTop(node, levelY);
                    schemaCanvas.Children.Add(node);
                    nodes[id] = (node, levelX, levelY);

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

            // Добавляем обработчики событий для плавного перемещения по холсту
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

                    // Свободное перемещение без ограничений
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

            // Добавляем обработчик для масштабирования с помощью CTRL + колесико мыши
            schemaCanvas.MouseWheel += (sender, e) =>
            {
                if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                {
                    double scaleFactor = e.Delta > 0 ? 1.1 : 0.9;
                    double newScaleX = scaleTransform.ScaleX * scaleFactor;
                    double newScaleY = scaleTransform.ScaleY * scaleFactor;

                    // Вычисляем минимальный масштаб, при котором схема помещается в видимую область
                    double viewWidth = schemaCanvas.ActualWidth;
                    double viewHeight = schemaCanvas.ActualHeight;
                    double minScaleX = viewWidth / schemaCanvas.Width;
                    double minScaleY = viewHeight / schemaCanvas.Height;
                    double minScale = Math.Max(minScaleX, minScaleY);

                    // Ограничиваем масштаб: минимум - чтобы схема помещалась, максимум - 5
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

        private string GetQuantityLabel(string quantityId, List<JObject> instances)
        {
            if (string.IsNullOrEmpty(quantityId)) return "";

            var quantity = instances.FirstOrDefault(i => i["id"]?.ToString() == quantityId && i["type"]?.ToString() == "measure_with_unit");
            if (quantity == null) return "";

            string value = quantity["attributes"]?["value_component"]?.ToString() ?? "";
            return value.Trim();
        }

        private string GetUnitForQuantity(string quantityId, List<JObject> instances)
        {
            if (string.IsNullOrEmpty(quantityId)) return "шт.";

            var quantity = instances.FirstOrDefault(i => i["id"]?.ToString() == quantityId && i["type"]?.ToString() == "measure_with_unit");
            if (quantity == null) return "шт.";

            string unitId = quantity["attributes"]?["unit_component"]?.ToString();
            var unit = instances.FirstOrDefault(i => i["id"]?.ToString() == unitId && i["type"]?.ToString() == "context_dependent_unit");
            string unitName = unit?["attributes"]?["id"]?.ToString() ?? "шт.";

            return unitName;
        }

        private string GetFriendlyLabel(string id, JObject instance, List<JObject> instances)
        {
            string type = instance["type"]?.ToString() ?? "unknown";
            string label = id;

            if (type.Contains("product_definition"))
            {
                string defId = instance["attributes"]?["id"]?.ToString() ?? "Unknown Definition";
                string formationId = instance["attributes"]?["formation"]?.ToString();

                if (!string.IsNullOrEmpty(formationId))
                {
                    var formation = instances.FirstOrDefault(i => i["id"]?.ToString() == formationId);
                    string productId = formation?["attributes"]?["of_product"]?.ToString();
                    string version = formation?["attributes"]?["id"]?.ToString() ?? "Unknown Version";

                    if (!string.IsNullOrEmpty(productId))
                    {
                        var product = instances.FirstOrDefault(i => i["id"]?.ToString() == productId);
                        string productCode = product?["attributes"]?["id"]?.ToString() ?? "Unknown Product";
                        string productName = product?["attributes"]?["name"]?.ToString() ?? "";

                        label = $"{defId} {productCode} {productName} версия {version}".Trim();
                    }
                    else
                    {
                        label = $"{defId} версия {version}";
                    }
                }
                else
                {
                    label = defId;
                }
            }
            else if (type == "eskd_product")
            {
                string productCode = instance["attributes"]?["id"]?.ToString() ?? "Unknown Product";
                string productName = instance["attributes"]?["name"]?.ToString() ?? "";
                label = $"{productCode} {productName}".Trim();
            }
            else if (type == "organization")
            {
                label = instance["attributes"]?["name"]?.ToString() ?? "Организация";
            }

            return label;
        }

        private UIElement CreateStyledEllipse(string type, string label)
        {
            var textBlock = new TextBlock
            {
                Text = label,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                FontSize = 12,
                MaxWidth = NodeWidth - 20,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

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
                Height = NodeHeight
            };
            container.Children.Add(ellipse);
            container.Children.Add(textBlock);

            textBlock.Measure(new Size(NodeWidth - 20, NodeHeight));
            double textWidth = textBlock.DesiredSize.Width;
            double textHeight = textBlock.DesiredSize.Height;
            Canvas.SetLeft(textBlock, (NodeWidth - textWidth) / 2);
            Canvas.SetTop(textBlock, (NodeHeight - textHeight) / 2);

            return container;
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