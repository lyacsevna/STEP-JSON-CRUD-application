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
        public List<TreeNode> FormatJsonObject(JObject jsonObject) => FormatJson(jsonObject);

        public List<TreeNode> FormatJson(JObject json)
        {
            var rootNodes = new List<TreeNode>();
            var rootNode = new TreeNode
            {
                Name = "=== ДАННЫЕ ===",
                IsExpanded = true,
                FontSize = 14
            };

            if (json["instances"] != null)
            {
                ProcessInstances(json["instances"] as JArray, rootNode);
            }
            else
            {
                ProcessJObject(json, rootNode);
            }

            rootNodes.Add(rootNode);
            return rootNodes;
        }

        private void ProcessInstances(JArray instances, TreeNode parentNode)
        {
            foreach (var instance in instances)
            {
                var instanceNode = new TreeNode
                {
                    Name = $"ID: {instance["id"]}",
                    Value = $"Тип: {instance["type"]}",
                    IsExpanded = true,
                    FontSize = 14,
                    Margin = new Thickness(10, 3, 0, 3)
                };

                if (instance["attributes"] is JObject attributes)
                {
                    var attrsNode = new TreeNode
                    {
                        Name = "Атрибуты",
                        IsExpanded = true,
                        FontSize = 14
                    };
                    ProcessFilteredAttributes(attributes, attrsNode);
                    instanceNode.Children.Add(attrsNode);
                }

                parentNode.Children.Add(instanceNode);
            }
        }

        private void ProcessFilteredAttributes(JObject attributes, TreeNode parentNode)
        {
            var allowedAttributes = new HashSet<string> { "id", "name", "description" };

            foreach (var property in attributes.Properties())
            {
                if (!allowedAttributes.Contains(property.Name.ToLower()))
                    continue;

                var node = new TreeNode
                {
                    FontSize = 14,
                    Margin = new Thickness(15, 2, 0, 2)
                };

                if (property.Name.ToLower() == "description")
                {
                    node.ImageSource = new BitmapImage(new Uri("pack://application:,,,/StaticFiles/description.png"));
                    node.Name = string.Empty;
                }
                else
                {
                    node.Name = property.Name;
                }

                switch (property.Value.Type)
                {
                    case JTokenType.Object:
                        node.Value = "{...}";
                        node.IsExpanded = true;
                        ProcessJObject((JObject)property.Value, node);
                        break;
                    case JTokenType.Array:
                        node.Value = $"[{property.Value.Count()}]";
                        node.IsExpanded = true;
                        ProcessArray((JArray)property.Value, node);
                        break;
                    default:
                        node.Value = property.Value.ToString();
                        break;
                }

                parentNode.Children.Add(node);
            }
        }

        private void ProcessJObject(JObject jObject, TreeNode parentNode)
        {
            foreach (var property in jObject.Properties())
            {
                var node = new TreeNode
                {
                    FontSize = 14,
                    Margin = new Thickness(15, 2, 0, 2)
                };

                if (property.Name.ToLower() == "description")
                {
                    node.ImageSource = new BitmapImage(new Uri("pack://application:,,,/StaticFiles/description.png"));
                    node.Name = string.Empty;
                }
                else
                {
                    node.Name = property.Name;
                }

                switch (property.Value.Type)
                {
                    case JTokenType.Object:
                        node.Value = "{...}";
                        node.IsExpanded = true;
                        ProcessJObject((JObject)property.Value, node);
                        break;
                    case JTokenType.Array:
                        node.Value = $"[{property.Value.Count()}]";
                        node.IsExpanded = true;
                        ProcessArray((JArray)property.Value, node);
                        break;
                    default:
                        node.Value = property.Value.ToString();
                        break;
                }

                parentNode.Children.Add(node);
            }
        }

        private void ProcessArray(JArray array, TreeNode parentNode)
        {
            for (int i = 0; i < array.Count; i++)
            {
                var node = new TreeNode
                {
                    Name = $"[{i}]",
                    FontSize = 14,
                    Margin = new Thickness(15, 2, 0, 2)
                };

                var item = array[i];
                if (item is JObject obj)
                {
                    node.Value = "{...}";
                    node.IsExpanded = true;
                    ProcessJObject(obj, node);
                }
                else if (item is JArray arr)
                {
                    node.Value = $"[{arr.Count}]";
                    node.IsExpanded = true;
                    ProcessArray(arr, node);
                }
                else
                {
                    node.Value = item.ToString();
                }

                parentNode.Children.Add(node);
            }
        }

        public void ExpandAllTreeViewItems(ItemsControl itemsControl)
        {
            foreach (var item in itemsControl.Items)
            {
                if (itemsControl.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem treeViewItem)
                {
                    treeViewItem.IsExpanded = true;
                    if (treeViewItem.Items.Count > 0)
                    {
                        ExpandAllTreeViewItems(treeViewItem);
                    }
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