namespace ACadSharp.IO.Templates
{
	internal interface ICadDictionaryTemplate : ICadObjectTemplate
	{
		public new CadObject CadObject { get; set; }
	}
}
