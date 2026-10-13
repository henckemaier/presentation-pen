using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;

namespace PresentationPen.Overlay
{
    public interface IUndoAction
    {
        void Undo();
        void Redo();
    }

    public sealed class UndoManager
    {
        private readonly Stack<IUndoAction> _undo = new();
        private readonly Stack<IUndoAction> _redo = new();

        /// <summary>True enquanto uma ação está sendo aplicada (para ignorar eventos gerados por ela).</summary>
        public bool IsApplying { get; private set; }

        /// <summary>Disparado sempre que o conteúdo muda por uma ação, desfazer ou refazer.</summary>
        public event Action? Changed;

        public void Push(IUndoAction action)
        {
            _undo.Push(action);
            _redo.Clear();
            Changed?.Invoke();
        }

        public void Undo()
        {
            if (_undo.Count == 0) return;
            var action = _undo.Pop();
            Apply(action.Undo);
            _redo.Push(action);
            Changed?.Invoke();
        }

        public void Redo()
        {
            if (_redo.Count == 0) return;
            var action = _redo.Pop();
            Apply(action.Redo);
            _undo.Push(action);
            Changed?.Invoke();
        }

        public void Apply(Action change)
        {
            IsApplying = true;
            try { change(); }
            finally { IsApplying = false; }
        }

        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
        }
    }

    public sealed class StrokesChangedAction : IUndoAction
    {
        private readonly StrokeCollection _target;
        private readonly StrokeCollection _added;
        private readonly StrokeCollection _removed;

        public StrokesChangedAction(StrokeCollection target, StrokeCollection added, StrokeCollection removed)
        {
            _target = target;
            _added = new StrokeCollection(added);
            _removed = new StrokeCollection(removed);
        }

        public void Undo()
        {
            if (_added.Count > 0) _target.Remove(_added);
            if (_removed.Count > 0) _target.Add(_removed);
        }

        public void Redo()
        {
            if (_removed.Count > 0) _target.Remove(_removed);
            if (_added.Count > 0) _target.Add(_added);
        }
    }

    public sealed class ElementAddedAction : IUndoAction
    {
        private readonly Canvas _canvas;
        private readonly UIElement _element;

        public ElementAddedAction(Canvas canvas, UIElement element)
        {
            _canvas = canvas;
            _element = element;
        }

        public void Undo() => _canvas.Children.Remove(_element);
        public void Redo() => _canvas.Children.Add(_element);
    }

    public sealed class ElementRemovedAction : IUndoAction
    {
        private readonly Canvas _canvas;
        private readonly UIElement _element;
        private readonly int _index;

        public ElementRemovedAction(Canvas canvas, UIElement element, int index)
        {
            _canvas = canvas;
            _element = element;
            _index = index;
        }

        public void Undo() => _canvas.Children.Insert(Math.Min(_index, _canvas.Children.Count), _element);
        public void Redo() => _canvas.Children.Remove(_element);
    }

    public sealed class ClearAction : IUndoAction
    {
        private readonly StrokeCollection _target;
        private readonly StrokeCollection _strokes;
        private readonly Canvas _canvas;
        private readonly List<UIElement> _elements;

        public ClearAction(StrokeCollection target, Canvas canvas)
        {
            _target = target;
            _strokes = new StrokeCollection(target);
            _canvas = canvas;
            _elements = new List<UIElement>();
            foreach (UIElement el in canvas.Children) _elements.Add(el);
        }

        public void Undo()
        {
            _target.Add(_strokes);
            foreach (var el in _elements) _canvas.Children.Add(el);
        }

        public void Redo()
        {
            _target.Remove(_strokes);
            foreach (var el in _elements) _canvas.Children.Remove(el);
        }
    }
}
