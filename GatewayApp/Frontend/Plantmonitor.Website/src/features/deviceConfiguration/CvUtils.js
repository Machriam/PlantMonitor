//        C1  C2  C3  C4  C++     JavaScript
// CV_8U   0   8  16  24  uchar   Uint8Array    ( 0..255 )
// CV_8S   1   9  17  25  char    Int8Array     ( -128..127 )
// CV_16U  2  10  18  26  ushort  Uint16Array   ( 0..65535 )
// CV_16S  3  11  19  27  short   Int16Array    ( -32768..32767 )
// CV_32S  4  12  20  28  int     Int32Array    ( -2147483648..2147483647 )
// CV_32F  5  13  21  29  float   Float32Array  ( -FLT_MAX..FLT_MAX, INF, NAN )
// CV_64F  6  14  22  30  double  Float64Array  ( -DBL_MAX..DBL_MAX, INF, NAN )

// eslint-disable-next-line @typescript-eslint/ban-ts-comment
// @ts-expect-error
export function printError(err) {
    if (typeof err === "undefined") {
        err = "";
    } else if (typeof err === "number") {
        if (!isNaN(err)) {
            if (typeof cv !== "undefined") {
                // eslint-disable-next-line no-undef
                err = "Exception: " + cv.exceptionFromPtr(err).msg;
            }
        }
    } else if (typeof err === "string") {
        let ptr = Number(err.split(" ")[0]);
        if (!isNaN(ptr)) {
            if (typeof cv !== "undefined") {
                // eslint-disable-next-line no-undef
                err = "Exception: " + cv.exceptionFromPtr(ptr).msg;
            }
        }
    }

    console.log(err);
}

// eslint-disable-next-line @typescript-eslint/ban-ts-comment
// @ts-expect-error
export function printMat(mat) {
    // eslint-disable-next-line @typescript-eslint/ban-ts-comment
    // @ts-expect-error
    console.log(this.matInfo(mat));
}

// eslint-disable-next-line @typescript-eslint/ban-ts-comment
// @ts-expect-error
export function matInfo(mat) {
    return {
        // eslint-disable-next-line @typescript-eslint/ban-ts-comment
        // @ts-expect-error
        _matType: this.matType(mat),
        _depth: mat.depth(),
        matSize: mat.matSize,
        _channels: mat.channels(),
        _size: mat.size(),
        _cols: mat.cols,
        _rows: mat.rows,
        //dims: mat.dims,
        _type: mat.type(),
        _elemSize: mat.elemSize(),
        _step: mat.step,
        //_empty: mat.empty(),
        // eslint-disable-next-line @typescript-eslint/ban-ts-comment
        // @ts-expect-error
        _data: this.matData(mat)
    };
}

// eslint-disable-next-line @typescript-eslint/ban-ts-comment
// @ts-expect-error
export function matType(mat) {
    let types = [
        "CV_8UC1",
        "CV_8SC1",
        "CV_16UC1",
        "CV_16SC1",
        "CV_32SC1",
        "CV_32FC1",
        "CV_64FC1",
        "CV_8UC2",
        "CV_8SC2",
        "CV_16UC2",
        "CV_16SC2",
        "CV_32SC2",
        "CV_32FC2",
        "CV_64FC2",
        "CV_8UC3",
        "CV_8SC3",
        "CV_16UC3",
        "CV_16SC3",
        "CV_32SC3",
        "CV_32FC3",
        "CV_64FC3",
        "CV_8UC4",
        "CV_8SC4",
        "CV_16UC4",
        "CV_16SC4",
        "CV_32SC4",
        "CV_32FC4",
        "CV_64FC4"
    ];

    return types[mat.type()];
}

// eslint-disable-next-line @typescript-eslint/ban-ts-comment
// @ts-expect-error
export function matData(mat) {
    let data;
    switch (mat.type() % 7) {
        case 0: // CV_8U
            data = mat.data;
            break;
        case 1: // CV_8S
            data = mat.data8S;
            break;
        case 2: // CV_16U
            data = mat.data16U;
            break;
        case 3: // CV_16S
            data = mat.data16S;
            break;
        case 4: // CV_32S
            data = mat.data32S;
            break;
        case 5: // CV_32F
            data = mat.data32F;
            break;
        case 6: // CV_64F
            data = mat.data64F;
            break;
        default:
            data = mat.data;
    }

    return data;
}

// eslint-disable-next-line @typescript-eslint/ban-ts-comment
// @ts-expect-error
export function matAtIdx(mat, idx) {
    let ptr = 0;
    for (let i = 0; i < idx.length; i++) {
        ptr += (idx[i] * mat.step[i]) / mat.elemSize();
    }

    // eslint-disable-next-line @typescript-eslint/ban-ts-comment
    // @ts-expect-error
    return this.matData(mat)[ptr];
}
