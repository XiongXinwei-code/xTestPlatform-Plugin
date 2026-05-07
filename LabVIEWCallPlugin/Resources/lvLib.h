#include "extcode.h"
#pragma pack(push)
#pragma pack(1)

#ifdef __cplusplus
extern "C" {
#endif
typedef uint32_t  Enum;
#define Enum_Bad 0
#define Enum_Idle 1
#define Enum_RunTopLevel 2
#define Enum_Running 3
typedef struct {
	int32_t dimSizes[2];
	uint32_t elt[1];
} Uint32ArrayBase;
typedef Uint32ArrayBase **Uint32Array;

/*!
 * LVfunction_GetConnectPanel
 */
void __cdecl LVfunction_GetConnectPanel(char VIpath[], char ControlIn[], 
	char IndicatorIn[], uint16_t Port, LVBoolean *IsReentrant, Enum *viState, 
	char ControlOut[], char IndicatorOut[], Uint32Array *_24BitPixmap, 
	int32_t len, int32_t len2);
/*!
 * LVfunction_EditVI
 */
int32_t __cdecl LVfunction_EditVI(char VIpath[], uint16_t Port);
/*!
 * LVfunction_GetLibItems
 */
void __cdecl LVfunction_GetLibItems(char lvLib_Path[], uint16_t Port, 
	char ItemPaths[], int32_t *error, int32_t len);
/*!
 * LVrte_GetParameters
 */
int32_t __cdecl LVrte_GetParameters(LVRefNum *reference, 
	char ParameterName[], LVRefNum *referenceOut, int16_t typeString[], 
	uint8_t dataString[], int32_t *error, int32_t len, int32_t len2);
/*!
 * LVrte_RunVI
 */
int32_t __cdecl LVrte_RunVI(LVRefNum *viReference, LVBoolean OpenPanel, 
	LVBoolean ClosePanel, LVRefNum *referenceOut, int32_t *error);
/*!
 * LVrte_LoadVI
 */
void __cdecl LVrte_LoadVI(uint16_t Port, char viPath[], 
	LVRefNum *viReference, int32_t *error);
/*!
 * LVrte_SetParameters
 */
int32_t __cdecl LVrte_SetParameters(LVRefNum *reference, 
	char ParameterName[], int16_t typeString[], uint8_t dataString[], 
	LVRefNum *referenceOut, int32_t *error, int32_t len, int32_t len2);
/*!
 * ConnectLabVIEW
 */
int32_t __cdecl ConnectLabVIEW(uint16_t Port);

MgErr __cdecl LVDLLStatus(char *errStr, int errStrLen, void *module);

/*
* Memory Allocation/Resize/Deallocation APIs for type 'Uint32Array'
*/
Uint32Array __cdecl AllocateUint32Array (int32 *dimSizeArr);
MgErr __cdecl ResizeUint32Array (Uint32Array *hdlPtr, int32 *dimSizeArr);
MgErr __cdecl DeAllocateUint32Array (Uint32Array *hdlPtr);

void __cdecl SetExecuteVIsInPrivateExecutionSystem(Bool32 value);

#ifdef __cplusplus
} // extern "C"
#endif

#pragma pack(pop)

