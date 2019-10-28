using UnityEngine;
using UnityEngine.InputNew;

// GENERATED FILE - DO NOT EDIT MANUALLY
namespace UnityEngine.Reflect
{
	public class ReflectMainMenuInput : ActionMapInput {
		public ReflectMainMenuInput (ActionMap actionMap) : base (actionMap) { }

		public ButtonInputControl @blockAction2 { get { return (ButtonInputControl)this[0]; } }
		public ButtonInputControl @selectItem { get { return (ButtonInputControl)this[1]; } }
		public AxisInputControl @navigateX { get { return (AxisInputControl)this[2]; } }
		public AxisInputControl @navigateY { get { return (AxisInputControl)this[3]; } }
		public Vector2InputControl @navigate { get { return (Vector2InputControl)this[4]; } }
		public ButtonInputControl @blockTrigger1 { get { return (ButtonInputControl)this[5]; } }
	}
}
