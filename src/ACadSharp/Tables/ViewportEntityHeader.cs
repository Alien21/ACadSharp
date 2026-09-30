namespace ACadSharp.Tables
{
	internal class ViewportEntityHeader : TableEntry
	{
		public override ObjectType ObjectType { get => ObjectType.VP_ENT_HDR; }

		public override string Name { get => this.name; set => this.name = value; }
	}
}
