using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace STEP_JSON_Application_for_ASKON
{
    public class TreeManager
    {
        // Configuration for flexibility (could be externalized)
        private readonly string InstancesKey = "instances";
        private readonly string TypeKey = "type";
        private readonly string AttributesKey = "attributes";
        private readonly string IdKey = "id";
        private readonly string NameKey = "name";
        private readonly string RelationKey = "next_assembly_usage_occurrence"; // Common relation type prefix
        private readonly string RelatingKey = "relating_product_definition";
        private readonly string RelatedKey = "related_product_definition";
        private readonly string QuantityKey = "quantity";
        private readonly string UnitKey = "measure_with_unit";
        private readonly string VersionKey = "formation";

        public List<TreeNode> FormatJsonObject(JObject jsonObject)
        {
            if (jsonObject == null)
                throw new ArgumentNullException(nameof(jsonObject));

            var rootNodes = new List<TreeNode>();

            // Check for instances array
            if (jsonObject[InstancesKey] is JArray instances)
            {
                rootNodes.AddRange(BuildTreeFromInstances(instances));
            }
            else
            {
                // Fallback to generic JSON processing
                var rootNode = new TreeNode
                {
                    Name = "=== ДАННЫЕ ===",
                    IsExpanded = true,
                    FontSize = 14
                };
                ProcessJObject(jsonObject, rootNode);
                rootNodes.Add(rootNode);
            }

            return rootNodes;
        }

        private List<TreeNode> BuildTreeFromInstances(JArray instances)
        {
            var rootNodes = new List<TreeNode>();

            // Find potential root nodes (no incoming relations)
            var referencedIds = instances
                .Where(i => i[TypeKey]?.ToString().Contains(RelationKey) == true)
                .Select(i => i[AttributesKey]?[RelatedKey]?.ToString())
                .Where(id => !string.IsNullOrEmpty(id))
                .ToHashSet();

            var rootCandidates = instances
                .Where(i => !referencedIds.Contains(i[IdKey]?.ToString()) &&
                            i[TypeKey]?.ToString().Contains("product") == true)
                .ToList();

            // If no clear root, use all product definitions or first instance
            if (!rootCandidates.Any())
            {
                rootCandidates = instances
                    .Where(i => i[TypeKey]?.ToString().Contains("product_definition") == true)
                    .ToList();
            }

            foreach (var root in rootCandidates)
            {
                var rootNode = CreateProductNode(root, instances, true);
                if (rootNode != null)
                    rootNodes.Add(rootNode);
            }

            // If no roots found, create a generic root
            if (!rootNodes.Any() && instances.Any())
            {
                var fallbackNode = new TreeNode
                {
                    Name = "=== СТРУКТУРА ===",
                    IsExpanded = true,
                    FontSize = 14
                };
                foreach (var instance in instances)
                {
                    var node = CreateGenericNode(instance, instances);
                    if (node != null)
                        fallbackNode.Children.Add(node);
                }
                rootNodes.Add(fallbackNode);
            }

            return rootNodes;
        }

        private TreeNode CreateProductNode(JToken product, JArray instances, bool isRoot = false)
        {
            if (product == null || product[IdKey] == null)
                return null;

            string productId = product[IdKey].ToString();
            var attributes = product[AttributesKey] as JObject;
            string defId = attributes?[IdKey]?.ToString() ?? "Unknown";
            string formationId = attributes?[VersionKey]?.ToString();

            var productNode = new TreeNode
            {
                Name = isRoot ? $"# {defId}" : defId,
                Value = GetProductDescription(product, instances),
                IsExpanded = true,
                FontSize = 14,
                Margin = new Thickness(0, 5, 0, 5)
            };

            // Find and add components
            var components = instances
                .Where(i => i[TypeKey]?.ToString().Contains(RelationKey) == true &&
                            i[AttributesKey]?[RelatingKey]?.ToString() == productId)
                .ToList();

            foreach (var component in components)
            {
                var componentNode = CreateComponentNode(component, instances);
                if (componentNode != null)
                {
                    productNode.Children.Add(componentNode);

                    // Recursively add nested products
                    string relatedId = component[AttributesKey]?[RelatedKey]?.ToString();
                    if (!string.IsNullOrEmpty(relatedId))
                    {
                        var relatedProduct = instances.FirstOrDefault(i => i[IdKey]?.ToString() == relatedId);
                        if (relatedProduct != null)
                        {
                            var nestedNode = CreateProductNode(relatedProduct, instances);
                            if (nestedNode != null)
                                componentNode.Children.Add(nestedNode);
                        }
                    }
                }
            }

            return productNode;
        }

        private TreeNode CreateComponentNode(JToken component, JArray instances)
        {
            var attributes = component[AttributesKey] as JObject;
            string refDesignator = attributes?["reference_designator"]?.ToString();
            string quantityId = attributes?[QuantityKey]?.ToString();

            string quantityLabel = GetQuantityLabel(quantityId, instances);
            string unit = GetUnitForQuantity(quantityId, instances);

            var componentNode = new TreeNode
            {
                Name = string.IsNullOrEmpty(refDesignator) ? "Состоит из" : $"Состоит из, поз.{refDesignator}",
                Value = $"кол-во {quantityLabel} {unit}",
                IsExpanded = true,
                FontSize = 14,
                Margin = new Thickness(10, 2, 0, 2)
            };

            return componentNode;
        }

        private TreeNode CreateGenericNode(JToken instance, JArray instances)
        {
            var attributes = instance[AttributesKey] as JObject;
            string id = instance[IdKey]?.ToString() ?? "Unknown";
            string type = instance[TypeKey]?.ToString() ?? "Unknown";
            string name = attributes?[NameKey]?.ToString() ?? attributes?[IdKey]?.ToString() ?? type;

            var node = new TreeNode
            {
                Name = name,
                Value = type,
                IsExpanded = false,
                FontSize = 12,
                Margin = new Thickness(5, 2, 0, 2)
            };

            // Add attributes as children
            if (attributes != null)
            {
                foreach (var attr in attributes.Properties())
                {
                    var attrNode = new TreeNode
                    {
                        Name = attr.Name,
                        Value = attr.Value.ToString(),
                        FontSize = 12,
                        Margin = new Thickness(10, 1, 0, 1)
                    };
                    node.Children.Add(attrNode);
                }
            }

            return node;
        }

        private string GetProductDescription(JToken product, JArray instances)
        {
            var attributes = product[AttributesKey] as JObject;
            string type = product[TypeKey]?.ToString() ?? "unknown";
            string defId = attributes?[IdKey]?.ToString() ?? "Unknown";
            string formationId = attributes?[VersionKey]?.ToString();

            if (type.Contains("product_definition") && !string.IsNullOrEmpty(formationId))
            {
                var formation = instances.FirstOrDefault(i => i[IdKey]?.ToString() == formationId);
                if (formation != null)
                {
                    string productId = formation[AttributesKey]?["of_product"]?.ToString();
                    string version = formation[AttributesKey]?[IdKey]?.ToString() ?? "Unknown";

                    if (!string.IsNullOrEmpty(productId))
                    {
                        var prod = instances.FirstOrDefault(i => i[IdKey]?.ToString() == productId);
                        if (prod != null)
                        {
                            string productCode = prod[AttributesKey]?[IdKey]?.ToString() ?? "Unknown";
                            string productName = prod[AttributesKey]?[NameKey]?.ToString() ?? "";
                            return $"{productCode} {productName} версия {version}".Trim();
                        }
                    }
                    return $"{defId} версия {version}";
                }
            }

            return attributes?[NameKey]?.ToString() ?? defId;
        }

        private string GetQuantityLabel(string quantityId, JArray instances)
        {
            if (string.IsNullOrEmpty(quantityId))
                return "неизвестно";

            var quantity = instances.FirstOrDefault(i => i[IdKey]?.ToString() == quantityId);
            return quantity?[AttributesKey]?["value_component"]?.ToString()?.Trim() ?? "неизвестно";
        }

        private string GetUnitForQuantity(string quantityId, JArray instances)
        {
            if (string.IsNullOrEmpty(quantityId))
                return "шт.";

            var quantity = instances.FirstOrDefault(i => i[IdKey]?.ToString() == quantityId);
            if (quantity == null)
                return "шт.";

            string unitId = quantity[AttributesKey]?["unit_component"]?.ToString();
            if (!string.IsNullOrEmpty(unitId))
            {
                var unit = instances.FirstOrDefault(i => i[IdKey]?.ToString() == unitId);
                return unit?[AttributesKey]?[IdKey]?.ToString() ?? "шт.";
            }

            return "шт.";
        }

        private void ProcessJObject(JObject jObject, TreeNode parentNode)
        {
            foreach (var property in jObject.Properties())
            {
                var childNode = new TreeNode
                {
                    Name = property.Name,
                    FontSize = 12,
                    Margin = new Thickness(5, 1, 0, 1)
                };

                if (property.Value is JObject childObject)
                {
                    childNode.IsExpanded = false;
                    ProcessJObject(childObject, childNode);
                }
                else if (property.Value is JArray childArray)
                {
                    childNode.IsExpanded = false;
                    ProcessArray(childArray, childNode);
                }
                else
                {
                    childNode.Value = property.Value.ToString();
                }

                parentNode.Children.Add(childNode);
            }
        }

        private void ProcessArray(JArray array, TreeNode parentNode)
        {
            for (int i = 0; i < array.Count; i++)
            {
                var childNode = new TreeNode
                {
                    Name = $"[{i}]",
                    FontSize = 12,
                    Margin = new Thickness(5, 1, 0, 1)
                };

                if (array[i] is JObject childObject)
                {
                    childNode.IsExpanded = false;
                    ProcessJObject(childObject, childNode);
                }
                else if (array[i] is JArray childArray)
                {
                    childNode.IsExpanded = false;
                    ProcessArray(childArray, childNode);
                }
                else
                {
                    childNode.Value = array[i].ToString();
                }

                parentNode.Children.Add(childNode);
            }
        }

        public void ExpandAllTreeViewItems(ItemsControl itemsControl)
        {
            if (itemsControl == null)
                return;

            foreach (var item in itemsControl.Items)
            {
                if (itemsControl.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem treeViewItem)
                {
                    treeViewItem.IsExpanded = true;
                    ExpandAllTreeViewItems(treeViewItem);
                }
            }
        }
    }

    public class TreeNode
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public List<TreeNode> Children { get; set; } = new List<TreeNode>();
        public bool IsExpanded { get; set; }
        public BitmapImage ImageSource { get; set; }
        public int FontSize { get; set; } = 12;
        public Thickness Margin { get; set; } = new Thickness(5);
    }
}