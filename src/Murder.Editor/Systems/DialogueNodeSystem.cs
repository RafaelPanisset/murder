﻿using Bang;
using Bang.Contexts;
using Bang.Systems;
using Murder.Core;
using Murder.Core.Dialogs;
using Murder.Core.Geometry;
using Murder.Core.Graphics;
using Murder.Core.Input;
using Murder.Editor.Components;
using Murder.Editor.Utilities;
using Murder.Editor.Utilities.Attributes;
using Murder.Services;
using Murder.Utilities;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Murder.Editor.Systems
{
    [DialogueEditor]
    [Filter(typeof(DialogueNodeEditorComponent))]
    internal class DialogueNodeSystem : IMonoRenderSystem, IUpdateSystem
    {
        private readonly Point _nodeSize = new(44f, 20);

        private int _dragging = -1;
        private int _hovering = -1;
        private float _zoom = 100;
        private List<Guid> _iconsCache = new List<Guid>(6);

        public void Update(Context context)
        {
            if (!context.HasAnyEntity)
                return;

            var editorComponent = context.Entity.GetComponent<DialogueNodeEditorComponent>();

            EditorHook hook = context.World.GetUnique<EditorComponent>().EditorHook;
            bool clicked = Game.Input.Pressed(MurderInputButtons.LeftClick);
            Vector2 cursorPosition = hook.CursorWorldPosition.ToVector2();

            // Try to pickup a node
            _hovering = -1;

            foreach (var node in editorComponent.Simulator.Nodes)
            {
                Rectangle rectangle = new(node.Position * _zoom, _nodeSize.ToVector2());
                if (rectangle.Contains(cursorPosition))
                {
                    _hovering = node.NodeId;
                    if (clicked)
                        _dragging = node.NodeId;
                }
            }

            if (_dragging >= 0)
            {
                editorComponent.Simulator.Nodes[_dragging].Position = (cursorPosition - _nodeSize/2f)/100f;

                if (!Game.Input.Down(MurderInputButtons.LeftClick))
                {
                    _dragging = -1;
                }
            }
        }
        
        public void Draw(RenderContext render, Context context)
        {
            if (!context.HasAnyEntity)
                return;

            var editorComponent = context.Entity.GetComponent<DialogueNodeEditorComponent>();


            float minDistance = 0.4f;
            float maxDistance = 0.9f;

            foreach (var node in editorComponent.Simulator.Nodes)
            {
                foreach (var other in editorComponent.Simulator.Nodes)
                {
                    if (other == node)
                        continue;

                    var delta = other.Position - node.Position;
                    var distance = delta.Length();
                    if (distance <= 0.0001f)
                    {
                        node.Position = new(node.Position.X - 0.01f, node.Position.Y + 0.01f);
                        other.Position = new(other.Position.X - 0.01f, other.Position.Y + 0.01f);
                    }
                    
                    if (delta.Length() < minDistance)
                    {
                        node.Speed -= GetPushSpeed(delta);
                        other.Speed += GetPushSpeed(delta);
                    }
                }
                node.Position += node.Speed * Game.DeltaTime;
                float maxValue = 10;
                node.Position = new Vector2(Math.Clamp(node.Position.X, -maxValue, maxValue), Math.Clamp(node.Position.Y, -maxValue, maxValue));
                node.Speed *= 0.2f;

                DrawNode(render, editorComponent.Situation, editorComponent.Situation.Dialogs[node.NodeId], new Point(node.Position.X * _zoom, node.Position.Y * _zoom));
            }

            var centerOffset = new Point(60 * 0.45f, 16);

            foreach (var edge in editorComponent.Situation.Edges)
            {
                var fromNode = editorComponent.Simulator.Nodes[edge.Key];

                foreach (var toNodeId in edge.Value.Dialogs)
                {
                    var toNode = editorComponent.Simulator.Nodes[toNodeId];
                    
                    // Pull towards the origin of the edge
                    var delta = fromNode.Position - toNode.Position;
                    var sign = new Vector2(Math.Sign(delta.X), Math.Sign(delta.Y));
                    if (delta.Length() > maxDistance)
                    {
                        toNode.Speed += sign * delta * delta * 0.3f;
                        fromNode.Speed -= sign * delta * delta * 0.3f;
                    }

                    Color lineColor = Color.Black;
                    Point center = (Vector2.Lerp(fromNode.Position, toNode.Position, 0.5f)*_zoom).Point + centerOffset;
                    switch (edge.Value.Kind)
                    {
                        case MatchKind.Next:
                            lineColor = Color.Orange;
                            RenderServices.DrawSprite(render.GameUiBatch, Game.Profile.EditorAssets.DialogueIconEdgeNext, "", center.X, center.Y,new DrawInfo() { Sort = 0.75f });
                            break;
                        case MatchKind.Random:
                            RenderServices.DrawSprite(render.GameUiBatch, Game.Profile.EditorAssets.DialogueIconEdgeRandom, "", center.X, center.Y,new DrawInfo() { Sort = 0.75f });
                            lineColor = Color.BrightGray;
                            break;
                        case MatchKind.HighestScore:
                            RenderServices.DrawSprite(render.GameUiBatch, Game.Profile.EditorAssets.DialogueIconEdgeScore, "", center.X, center.Y,new DrawInfo() { Sort = 0.75f });
                            lineColor = Color.BrightGray;
                            break;
                        case MatchKind.IfElse:
                            RenderServices.DrawSprite(render.GameUiBatch, Game.Profile.EditorAssets.DialogueIconEdgeIf, "", center.X, center.Y,new DrawInfo() { Sort = 0.75f });
                            lineColor = Color.Orange;
                            break;
                        case MatchKind.Choice:
                            RenderServices.DrawSprite(render.GameUiBatch, Game.Profile.EditorAssets.DialogueIconEdgeChoice, "", center.X, center.Y,new DrawInfo() { Sort = 0.75f });
                            lineColor = Color.BrightGray;
                            break;
                        default:
                            break;
                    }
                    render.GameUiBatch.DrawLine(fromNode.Position * _zoom + centerOffset, toNode.Position * _zoom + centerOffset, lineColor, 2f, 0.8f);
                }
            }

        }

        private static Vector2 GetPushSpeed(Vector2 delta)
        {
            var clampedDelta = new Vector2(
                Calculator.ClampNearZero(delta.X, 0.05f),
                Calculator.ClampNearZero(delta.Y, 0.05f)
                );
            var ret = Vector2.One / clampedDelta;

            return ret * 0.01f;
        }

        private void DrawNode(RenderContext render, Situation situation, Dialog info, Point point)
        {
            var box = (info.Id == _hovering)? Game.Profile.EditorAssets.BoxBgSelected : Game.Profile.EditorAssets.BoxBg;
            _iconsCache.Clear();

            if (info.Id == 0)
            {
                _iconsCache.Add(Game.Profile.EditorAssets.DialogueIconHello);
            }

            if (info.Lines.Length > 0)
            {
                _iconsCache.Add(Game.Profile.EditorAssets.DialogueIconBaloon);
            }

            if (info.Actions.HasValue)
            {
                _iconsCache.Add(Game.Profile.EditorAssets.DialogueIconAction);
            }

            if (info.GoTo.HasValue)
            {
                _iconsCache.Add(Game.Profile.EditorAssets.DialogueIconFlow);
            }
            else if (!situation.Edges.ContainsKey(info.Id))
            {
                _iconsCache.Add(Game.Profile.EditorAssets.DialogueIconExit);
            }

            for (int i = 0; i < _iconsCache.Count; i++)
            {
                RenderServices.DrawSprite(render.GameUiBatch, _iconsCache[i], "", Calculator.RoundToInt(point.X + _nodeSize.X/2f + i * 16 - 8*_iconsCache.Count), point.Y + 1,
                    new DrawInfo() { Sort = 0.45f });
            }

            box.Draw(render.GameUiBatch, new Rectangle(point.X, point.Y, _nodeSize.X, _nodeSize.Y), new DrawInfo() { Sort = 0.5f });
        }
    }
}
