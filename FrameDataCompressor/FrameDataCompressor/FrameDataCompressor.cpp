#include "stdafx.h"

#include <string>
#include <vector>
#include <fstream>
#include <sstream>
#include <iostream>

#include "zlib.h"
#include "dirent.h"

using namespace std;

void getFrameData(string dirPath, int& frameCount, int& particleCount)
{
	DIR *dir;
	struct dirent *entry;
	
	if ((dir = opendir(dirPath.c_str())) != NULL)
	{
		while ((entry = readdir(dir)) != NULL)
		{
			string strEntry = entry->d_name;
			size_t findRes = strEntry.find(".dat");

			if (findRes != string::npos)
			{
				frameCount ++;

				if(particleCount == 0)
				{
					string filePath = dirPath + strEntry;
					string dummy;
					ifstream File(filePath);

					if (File)
					{
						while (getline(File, dummy))
						{
							particleCount ++;
						}
					}
				}				
			}		
		}

		closedir(dir);
	}
}

int _tmain(int argc, TCHAR *argv[])
{
	string dirPath = ".\\";	
	string dataFilePath = dirPath + "data.bin";
	string indexFilePath = dirPath + "index.bin";

	DIR *dir;
	struct dirent *entry;

	int frameCount = 0;
	int particleCount = 0;
	getFrameData(dirPath, frameCount, particleCount);
	const size_t PARTICLE_FRAME_SIZE = 32;	

	unsigned char* uncompressedFrame = new unsigned char[particleCount * PARTICLE_FRAME_SIZE];
	unsigned char* compressedFrame = new unsigned char[particleCount * PARTICLE_FRAME_SIZE];

	fill(compressedFrame, compressedFrame + sizeof(compressedFrame), 0);
	fill(uncompressedFrame, uncompressedFrame + sizeof(uncompressedFrame), 0);

	cout << "Starting compression..." << endl;
	cout << "Directory: " << dirPath  << endl;
	cout << "Frame count: " << frameCount << endl;
	cout << "Particle count: " << particleCount << endl;	

	string line;
	size_t tokenIndices[7];

	char* ping_c_str;
	char* pong_c_str;

	float type, id, x, y, z;

	unsigned long long* frameIndices = new unsigned long long[frameCount];
	
	ofstream dataFile;
	ofstream indexFile;

	dataFile.open(dataFilePath, ios::binary);
	

	if ((dir = opendir(dirPath.c_str())) != NULL)
	{
		int frameCounter = 0;

		// Print all the files and directories within directory 
		while ((entry = readdir(dir)) != NULL)
		{
			//cout << "File: " + strEntry << endl;

			if (frameCounter >= frameCount) break;

			string strEntry = entry->d_name;
			size_t findRes = strEntry.find(".dat");
			
			if (findRes == string::npos)
			{
				//cout << "Skip directory entry: " << entry->d_name << endl;
				continue;
			}			
			
			string filePath = dirPath + strEntry;
			ifstream File(filePath);
			if (File)
			{
				if (frameCounter %100 == 0) cout << "Compressing frame: " << frameCounter << " out of: " << frameCount << endl;
								
				int particleCounter = 0;
				
				while (getline(File, line))
				{
					type = strtod(line.c_str(), &ping_c_str);
					id = strtod(ping_c_str, &pong_c_str);
					x = strtod(pong_c_str, &ping_c_str);
					y = strtod(ping_c_str, &pong_c_str);
					z = strtod(pong_c_str, &ping_c_str);

					unsigned char *ptype = (unsigned char *)&type;
					std::copy(ptype, ptype + sizeof(float), uncompressedFrame + PARTICLE_FRAME_SIZE * particleCounter + 0 * sizeof(float));

					unsigned char *pid = (unsigned char *)&id;
					std::copy(pid, pid + sizeof(float), uncompressedFrame + PARTICLE_FRAME_SIZE * particleCounter + 1 * sizeof(float));

					unsigned char *px = (unsigned char *)&x;
					std::copy(px, px + sizeof(float), uncompressedFrame + PARTICLE_FRAME_SIZE * particleCounter + 2 * sizeof(float));
					
					unsigned char *py = (unsigned char *)&y;
					std::copy(py, py + sizeof(float), uncompressedFrame + PARTICLE_FRAME_SIZE * particleCounter + 3 * sizeof(float));

					unsigned char *pz = (unsigned char *)&z;
					std::copy(pz, pz + sizeof(float), uncompressedFrame + PARTICLE_FRAME_SIZE * particleCounter + 4 * sizeof(float));

					/*type = *(float*)(uncompressedFrame + PARTICLE_FRAME_SIZE * particleCounter + 0 * sizeof(float));
					id = *(float*)(uncompressedFrame + PARTICLE_FRAME_SIZE * particleCounter + 1 * sizeof(float));
					x = *(float*)(uncompressedFrame + PARTICLE_FRAME_SIZE * particleCounter + 2 * sizeof(float));
					y = *(float*)(uncompressedFrame + PARTICLE_FRAME_SIZE * particleCounter + 3 * sizeof(float));
					z = *(float*)(uncompressedFrame + PARTICLE_FRAME_SIZE * particleCounter + 4 * sizeof(float));

					cout << type << " : " << id << " : " << x << " : " << y << " : " << z << endl;*/
					
					particleCounter++;
				}

				File.close();
				
				uLongf compressedFrameSize = particleCount * PARTICLE_FRAME_SIZE;				
				int res = compress(compressedFrame, &compressedFrameSize, uncompressedFrame, particleCount * PARTICLE_FRAME_SIZE);
				
				///*cout << "Uncompressed frame size: " << PARTICLE_COUNT * PARTICLE_FRAME_SIZE << endl;
				//cout << "Compressed frame size: " << compressedFrameSize << endl;*/

				//// Store current frame index
				if (frameCounter == 0) frameIndices[frameCounter] = compressedFrameSize;
				else frameIndices[frameCounter] = frameIndices[frameCounter - 1] + compressedFrameSize;				

				dataFile.write(reinterpret_cast<char*>(compressedFrame), compressedFrameSize);

				frameCounter++;
			}			
		}
		closedir(dir);

		// Close data.bin
		dataFile.close();
		
		// Open index.bin
		indexFile.open(indexFilePath, ios::binary);
		indexFile.write(reinterpret_cast<char*>(frameIndices), frameCount * sizeof(unsigned long long));
		indexFile.close();
	}

	return 0;	
}

