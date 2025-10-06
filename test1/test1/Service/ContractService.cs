using System.IO;
using System;
using Microsoft.AspNetCore.Hosting;

namespace test1.Service
{
	public class ContractService
	{
		private readonly string _basePath;

		public ContractService(IWebHostEnvironment env)
		{
			// env.ContentRootPath là đường dẫn gốc của dự án,
			// thường là nơi chứa file .csproj
			_basePath = env.ContentRootPath;
		}

		public string GetEscrowAbi()
		{
			var abiPath = Path.Combine(_basePath, "SmartContracts", "Escrow.abi");
			return File.ReadAllText(abiPath);
		}

		public string GetEscrowBytecode()
		{
			var bytecodePath = Path.Combine(_basePath, "SmartContracts", "Escrow.bin");
			return File.ReadAllText(bytecodePath);
		}
	}
}
