using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace STEP_JSON_Application_for_ASKON
{
    public class TreeManager
    {
        // Сохраняем старый метод для совместимости
        public List<TreeNode> FormatJsonObject(JObject jsonObject)
        {
            return FormatJson(jsonObject);
        }

        // Новый метод (основная реализация)
        public List<TreeNode> FormatJson(JObject json)
        {
            var rootNodes = new List<TreeNode>();

            // Обработка корневого уровня
            var rootNode = new TreeNode { Name = "=== ДАННЫЕ ===", IsExpanded = true };

            // Если есть массив instances (как в исходном коде)
            if (json["instances"] != null)
            {
                ProcessInstances(json["instances"] as JArray, rootNode);
            }
            else
            {
                // Обработка обычного JSON
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
                    IsExpanded = true
                };

                if (instance["attributes"] is JObject attributes)
                {
                    var attrsNode = new TreeNode { Name = "Атрибуты", IsExpanded = true };
                    ProcessJObject(attributes, attrsNode);
                    instanceNode.Children.Add(attrsNode);
                }

                parentNode.Children.Add(instanceNode);
            }
        }

        private void ProcessJObject(JObject jObject, TreeNode parentNode)
        {
            foreach (var property in jObject.Properties())
            {
                var node = new TreeNode { Name = property.Name };

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
                var item = array[i];
                var node = new TreeNode { Name = $"[{i}]" };

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
}