using System;
using System.Text;
using L10N;
using UnityEngine;

namespace Durango.UI.Control;

public class PrerequisiteLoader : MonoBehaviour
{
	[SerializeField]
	private UISlider _loadingSlider;

	[SerializeField]
	private UILabel _loadingProgress;

	private float _lastFileProgress;

	private string _lastFileName;

	private int _lastCount;

	private bool _raised;

	public int TotalCount { get; set; }

	public void DetailedProgressChanged(float progress)
	{
		_lastFileProgress = progress;
		UpdateLoadingProgress();
	}

	public void ProgressChanged(int count, int retryCount, string fileName)
	{
		_lastFileProgress = 0f;
		int num = fileName.LastIndexOf("$", StringComparison.Ordinal);
		if (num >= 0)
		{
			fileName = fileName.Substring(num + 1, fileName.Length - num - 1);
		}
		if (retryCount > 0)
		{
			StringBuilder stringBuilder = new StringBuilder(fileName);
			stringBuilder.Append('.', retryCount % 5 + 1);
			fileName = stringBuilder.ToString();
		}
		_lastFileName = fileName;
		_lastCount = count;
		_loadingSlider.value = ((TotalCount <= 0) ? 1f : ((float)_lastCount / (float)TotalCount));
		UpdateLoadingProgress();
	}

	public void SetProgress(float value, string text)
	{
		if (!_raised)
		{
			_raised = true;
			if (_loadingSlider.foregroundWidget != null)
			{
				_loadingSlider.foregroundWidget.depth = 1010;
			}
			_loadingProgress.depth = 1011;
		}
		_loadingSlider.value = Mathf.Clamp01(value);
		_loadingProgress.text = text;
	}

	private void UpdateLoadingProgress()
	{
		string text = T._("다운로드 중: {0} / {1}", _lastCount, TotalCount);
		_loadingProgress.text = text;
	}
}
