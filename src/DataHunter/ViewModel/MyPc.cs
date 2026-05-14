using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DataHunter.Annotations;

namespace DataHunter.ViewModel
{
	public class MyPc : INotifyPropertyChanged
	{
		public MyPc(List<Drive> drives)
		{
			Drives = drives;
		}

		public event PropertyChangedEventHandler PropertyChanged;

		public string Name => "This PC";

		public List<Drive> Drives { get; }

		public bool IsExpanded
		{
			get
			{
				return isExpanded;
			}
			set
			{
				if(isExpanded == value)
					return;

				isExpanded = value;
				OnPropertyChanged();
			}
		}

		public bool IsSelected
		{
			get
			{
				return isSelected;
			}
			set
			{
				if(isSelected == value)
					return;

				isSelected = value;
				OnPropertyChanged();
			}
		}

		[NotifyPropertyChangedInvocator]
		private void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		private bool isExpanded = true;
		private bool isSelected = true;
	}
}
