/*
<!-- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -->
<!-- code copyright (c) 2016-2017 ProcGenesis                            -->
<!-- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -->
*/

importScripts('draw.min.js', 'perlin.js', 'seedrandom.js', 'rhill-voronoi-core.js');



var plate_count, plate_coords, plate_coords_wrapped, vor_result, vor_result_wrapped,
    bbox, bbox_wrapped, PlateIdentifierArrayWrap, PlateIdentifierArrayWrapCrop,
    PlateTurbArray2, PlateEdgeArray, PlateAttributeArray, PlateBoundaryStressArray, plate_neighbors,
    PerlinElevationArray;
var voronoi, voronoi_wrap;
var rockArray, oreArray, sea_level;
var elArray, stArray, attArray, neighArray;
var width, height;
var savedmrandstate;


self.addEventListener('message', function (e)
{
    var data = e.data;
    /*switch (data.cmd) {
    case 'start':
    self.postMessage('WORKER STARTED: ' + data.msg);
    break;
    case 'stop':
    self.postMessage('WORKER STOPPED: ' + data.msg +
    '. (buttons will no longer work)');
    self.close(); // Terminates the worker.
    break;
    default:
    self.postMessage('Unknown command: ' + data.msg);
    };*/


    //console.log("Data load function within worker");

    //elArray = data.elevationArr;
    //stArray = data.stressArr;
    //attArray = data.attributeArr;
    //neighArray = data.neighborArr;
    width = data.width;
    height = data.height;
    //rockArray = data.rockArr;
    //oreArray = data.oreArr;
    sea_level = data.sl;
    savedmrandstate = data.savedmrand;

    plate_count = data.plate_count;
    //voronoi = data.voronoi;
    //voronoi_wrap = data.voronoi_wrap;

    rockArray = matrix(width, height, "");
    oreArray = matrix(width, height, "");

    plate_coords = [];
    plate_coords_wrapped = [];

    bbox = { xl: 0, xr: width, yt: 0, yb: height };
    bbox_wrap = { xl: 0, xr: width * 2, yt: 0, yb: height };

    voronoi = new Voronoi();
    voronoi_wrap = new Voronoi();

    plate_neighbors = [];

    Math.seedrandom('', { state: savedmrandstate });

    //console.log("create points pc " + plate_count);
    for (var i = 0; i < plate_count; i++)
    {
        plate_coords[i] = { x: Math.floor(Math.random() * (width - 1)), y: Math.floor(Math.random() * (height - 1)) };
        plate_coords_wrapped[i] = { x: plate_coords[i].x + width / 2, y: plate_coords[i].y };                       //create set of offset points to calculate wraparound

        if (plate_coords[i].x < width / 2)
        {
            plate_coords_wrapped[i + plate_count] = { x: plate_coords_wrapped[i].x + width, y: plate_coords[i].y };

        }
        else if (plate_coords[i].x >= width / 2)
        {
            plate_coords_wrapped[i + plate_count] = { x: plate_coords_wrapped[i].x - width, y: plate_coords[i].y };
        }
    }
    //console.log("PC/PCW");
    //console.log(plate_coords);
    //console.log(plate_coords_wrapped);

    postMessage({ 'isData': false, 'setup': true, 'percent': 1 / 22 });

    //---------------Create Iniital Voronoi Cells, Create 2*Width Voronoi Cells for Wrapping, Draw Them----------------
    vor_result = voronoi.compute(plate_coords, bbox);
    postMessage({ 'isData': false, 'setup': true, 'percent': 3 / 22 });
    vor_result_wrapped = voronoi_wrap.compute(plate_coords_wrapped, bbox_wrap);
    postMessage({ 'isData': false, 'setup': true, 'percent': 5 / 22 });

    //---------------Give all pixels in a plate the same integer value, then crop the 2*width image to mimic wraparound-------------

    PlateIdentifierArrayWrap = setWrapPlateIDs(vor_result_wrapped, width * 2, height);
    postMessage({ 'isData': false, 'setup': true, 'percent': 8 / 22 });
    PlateIdentifierArrayWrapCrop = cropWrapPlateIDs(PlateIdentifierArrayWrap, width, height);
    postMessage({ 'isData': false, 'setup': true, 'percent': 11 / 22 });


    //---------------Add Turbulence to Plates, Get Edges from Plates-------------------------

    PlateTurbArray2 = addVorTurbulence(PlateIdentifierArrayWrapCrop, width, height, 56);
    postMessage({ 'isData': false, 'setup': true, 'percent': 12 / 22 });
    PlateEdgeArray = getEdges(PlateTurbArray2, width, height);
    postMessage({ 'isData': false, 'setup': true, 'percent': 13 / 22 });
    PlateAttributeArray = setPlateAttributes(PlateTurbArray2, PlateEdgeArray, width, height, .5, plate_count);
    postMessage({ 'isData': false, 'setup': true, 'percent': 14 / 22 });

    PlateBoundaryStressArray = setPlateBoundaryStress_worker(PlateAttributeArray, PlateEdgeArray, plate_coords, width, height);
    //postMessage({ 'isData': false, 'setup': true, 'percent': 19 / 22 });
    plate_neighbors = findPlateNeighbors(PlateBoundaryStressArray, width, height)
    //postMessage({ 'isData': false, 'setup': true, 'percent': 20 / 22 });

    var pn = new Perlin(Math.random());
    postMessage({ 'isData': false, 'setup': true, 'percent': 17 / 22 });
    PerlinElevationArray = createPerlinElevation_worker(pn, width, height);  //rewrite to include # of octaves, freq, weighting, etc
    postMessage({ 'isData': false, 'setup': true, 'percent': 22 / 22 });

    postMessage({ 'isData': true, 'setup': true, 'plate_coords': JSON.stringify(plate_coords), /*'plate_coords_wrapped': plate_coords_wrapped, 'vor_result': vor_result, 'vor_result_wrapped': vor_result_wrapped,
    'PlateIdentifierArrayWrap': PlateIdentifierArrayWrap, 'PlateIdentifierArrayWrapCrop': PlateIdentifierArrayWrapCrop,*/
        'PlateTurbArray2': JSON.stringify(PlateTurbArray2), /*'PlateEdgeArray': PlateEdgeArray,*/'PlateBoundaryStressArray': JSON.stringify(PlateBoundaryStressArray), 'PlateAttributeArray': JSON.stringify(PlateAttributeArray)/*, 'plate_neighbors': plate_neighbors /*'PerlinElevationArray': PerlinElevationArray, 'savedRand': Math.random.state()*/
    });
    //generateMap2();

    modifyElevation_worker(PerlinElevationArray, PlateBoundaryStressArray, PlateAttributeArray, plate_neighbors, width, height, rockArray, oreArray);






    //modifyElevation_worker(elArray, stArray, attArray, neighArray, wid, ht, rockArray, oreArray);
    //console.log("ELARRAY " + elArray);
    //postMessage(attArray);

}, false);

function createPerlinElevation_worker(pn, width, height)
{

    var ElevationArray = matrix(width, height, 0);
    var nx, ny, nz;
    var value;

    for (var y = 0; y < height; y++)
    {
        for (var x = 0; x < width; x++)
        {   

            //var nx = 6*(x)/width; 
            ny = 4*y / height;
            //nx = Math.cos(2 * Math.PI * (x / width));
            //nx = nx % width;
            nx = Math.cos((x * 2 * Math.PI) / width); 
            nz = Math.sin((x * 2 * Math.PI) / width);

            //Create Elevation Noise
            //var value = pn.noise(nx, ny, 0) + .5 * pn.noise(2 * nx, 2 * ny, 0) + .25 * pn.noise(4 * nx, 4 * ny, 0) + .125 * pn.noise(8 * nx, 8 * ny, 0) + .0625 * pn.noise(16 * nx, 16 * ny, 0);
            value = pn.noise(nx, ny, nz) + .5 * pn.noise(2 * nx, 2 * ny, 2*nz) + .25 * pn.noise(4 * nx, 4 * ny, 4*nz) + .125 * pn.noise(8 * nx, 8 * ny, 8*nz) + .0625 * pn.noise(16 * nx, 16 * ny, 16*nz);

            //value /= (1 + .5 + .25 + .125 + .0625);
            value /= 1.28;
            value = Math.pow(value, 2);

            ElevationArray[x][y] = value;
            if (x == 0)
            {
                postMessage({ 'isData': false, 'setup': true, 'percent': (17/22) + (y * width + x) / (width * height) * (5 / 22) });
            }
        }
    }

    return ElevationArray;
}
//function modifyElevation_worker(elevation_array, stress_array, attribute_array, neighbor_array, width, height, RockMap, OreMap)
//{
  

//}


function setPlateBoundaryStress_worker(attribute_array, edge_array, plate_coords_array, width, height)
{
    var stress_array = matrix(width, height, 0);
    var ordered_plates = [];
    var wrapxl = 0;
    var wrapxr = 0;
    var wrapyu = 0;
    var wrapyb = 0;
    var lowpar = 1;
    var highpar = 0;
    var lowperp = 1;
    var highperp = 0;

    var edgeonlyarray = [];

    var repeat_check = [];
    var result = {};
    //var doublecount = 0;
    var doublecount = [];
    //console.log("Plate Coords Array length");
    //console.log(plate_coords_array.length);

    //console.log(attribute_array);

    //var missingplatecoord = 0;
    var missingplatecoords = [];

    for (var p = 0; p < plate_coords_array.length; p++)
    {
        repeat_check.push(attribute_array[plate_coords_array[p].x][plate_coords_array[p].y].id);
        //missingplatecoords.push(p);
    }
    for (var i = 0; i < repeat_check.length; i++ )
    {
        if(!result[repeat_check[i]])
        {
            result[repeat_check[i]] = 0;
        }
        else
        {
           //missingplatecoord = i;
           missingplatecoords.push(i);  
        }
        ++result[repeat_check[i]];
    }
    //console.log("Repeat Check + Result");
    //console.log(repeat_check);
    //console.log(result);

    //var doubled_id = 0;
    //var missing_id = 0;
    var doubled_ids = [];
    var missing_ids = [];

    //console.log("result length " + result.length);

    for(var i = 0; i < repeat_check.length; i++)
    {
        //console.log("result[i]: " + result[i]);
        if(result[i] > 1)
        {
            //doubled_id = i;
            doubled_ids.push(i);
        }
        if(!result[i])
        {
            //missing_id = i;
            missing_ids.push(i);
        }
    }

    //console.log("double id/missing id");
    //console.log(doubled_ids + " " + missing_ids);


    //console.log("doubled ids/missing ids/missingplatecoords/platecoordsarray");
    //console.log(doubled_id + " " + missing_id);
    //console.log(doubled_ids);
    //console.log(missing_ids);
    //console.log(missingplatecoords);
    //console.log(plate_coords_array);

    //console.log("doubled ids");
    //console.log(doubled_ids);

    outerLoop:
    for (var p = 0; p < plate_coords_array.length; p++ )
    {
        //console.log("p: " + p);
           //console.log("PCA X, PCA Y #"+p);
           //console.log(plate_coords_array[p].x + ", " + plate_coords_array[p].y);
           //console.log("Att Array @ PCAX, PCAY");
           //console.log(attribute_array[plate_coords_array[p].x][plate_coords_array[p].y]);

           //if(repeat_check.includes(attribute_array[plate_coords_array[p].x][plate_coords_array[p].y].id))

        for (var d = 0; d < doubled_ids.length; d++)
        {
            //console.log("d/doublecount "+d);
            //console.log(doublecount);
            if (repeat_check[p] == doubled_ids[d])
            {
                //console.log("rc[p] == di[d]");
                //console.log(doublecount[doubled_ids[d]]);
                //console.log("repeat check p == doubled ids d");
                if(!doublecount[doubled_ids[d]])
                {
                   doublecount[doubled_ids[d]] = 0; 
                }
                doublecount[doubled_ids[d]]++;
            }

            if (doublecount[doubled_ids[d]] > 1)
            {
                //console.log("doublecount > 1, d: " + d + "missing_ids[d]: "+missing_ids[d]);
                //console.log("doublecount > 1");
                if (ordered_plates[missing_ids[d]])
                {
                    var d2 = d + 1;
                    while(ordered_plates[missing_ids[d2]])
                    {
                        
                        d2++;    
                    }
                    ordered_plates[missing_ids[d2]] = { x: plate_coords_array[missingplatecoords[d2]].x, y: plate_coords_array[missingplatecoords[d2]].y };

                }
                else
                {
                    ordered_plates[missing_ids[d]] = { x: plate_coords_array[missingplatecoords[d]].x, y: plate_coords_array[missingplatecoords[d]].y };
                }
                doublecount[doubled_ids[d]] = 1;
                continue outerLoop;
            }
        }
           ordered_plates[attribute_array[plate_coords_array[p].x][plate_coords_array[p].y].id] = {x: plate_coords_array[p].x, y: plate_coords_array[p].y};  //sometimes throwing error, "cannot read property y of undefined"
           //repeat_check.push(attribute_array[plate_coords_array[p].x][plate_coords_array[p].y].id);
    }


    //console.log("OP");
    //console.log(ordered_plates);
    //console.log("Ordered Plates");
    //console.log(ordered_plates);
    for(var y = 0; y < height; y++)
    {
        for(var x = 0; x < width; x++)
        {


            wrapxl = x - 1;
            wrapxr = x + 1;
            wrapyu = y - 1;
            wrapyb = y + 1;

            
            if(x - 1 < 0)
            {
                //wrapxl = width - x;
                wrapxl = width - 1;
            }
            if(x + 1 >= width)
            {
                wrapxr = 0;
                //wrapxr = x % width - 1;
            }
            if(y - 1 < 0)                   //y doesn't wrap, so don't sample values on other side of image
            {
                wrapyu = y;
                //wrapyu = height - y;
            }
            if(y + 1 >= height)
            {
                wrapyb = y;
                //wrapyb = y % height - 1;
            }

            var conditions = [attribute_array[wrapxl][wrapyu], attribute_array[x][wrapyu], attribute_array[wrapxr][wrapyu], attribute_array[wrapxl][y], attribute_array[wrapxr][y], attribute_array[wrapxl][wrapyb], attribute_array[x][wrapyb], attribute_array[wrapxr][wrapyb]];

            //if (x < 100)
            //{
            //    console.log(x + ", " + y);
            //    console.log(conditions);
            //}
            var neighborx = 0;
            var neighbory = 0;

            for(var i = 0; i < conditions.length; i++)
            {
                //console.log(conditions[i].isOceanic);
                if(conditions[i].id != attribute_array[x][y].id)
                {   

                    switch(i)
                    {
                        case 0:
                            neighborx = wrapxl;
                            neighbory = wrapyu;
                            break;

                        case 1:
                            neighborx = x;
                            neighbory = wrapyu;
                            break;
                        
                        case 2:
                            neighborx = wrapxr;
                            neighbory = wrapyu;
                            break;
                        
                        case 3:
                            neighborx = wrapxl;
                            neighbory = y;
                            break;

                        case 4:
                            neighborx = wrapxr;
                            neighbory = y;
                            break;

                        case 5:
                            neighborx = wrapxl;
                            neighbory = wrapyb;
                            break;

                        case 6:
                            neighborx = x;
                            neighbory = wrapyb;
                            break;

                        case 7:
                            neighborx = wrapxr;
                            neighbory = wrapyb;
                            break;

                        default:
                            neighborx = x;
                            neighbory = y;
                    }
                    //var slopey = plate_coords_array[attribute_array[x][y].id].y - plate_coords_array[conditions[i].id].y;       //find slope of line between the two plate coordinate points
                    //var slopex = plate_coords_array[attribute_array[x][y].id].x - plate_coords_array[conditions[i].id].x;
                    //var slopey = plate_coords_array[conditions[i].id].y - plate_coords_array[attribute_array[x][y].id].y;
                    //var slopex = plate_coords_array[conditions[i].id].x - plate_coords_array[attribute_array[x][y].id].x;
                    //console.log("ordered plates");
                    //console.log(ordered_plates);
                    //console.log("conditions");
                    //console.log(conditions);
                    //console.log("conditions[i].id");
                    //console.log(conditions[i].id);
                    //console.log("conditions[i].id " + conditions[i].id);
                    //console.log("OP @ above: " + ordered_plates[conditions[i].id]);

                    var slopey = ordered_plates[conditions[i].id].y - ordered_plates[attribute_array[x][y].id].y;
                    var slopex = ordered_plates[conditions[i].id].x - ordered_plates[attribute_array[x][y].id].x;

                    

                    //var slope;
                    //if(slopex != 0)
                    //{
                    //    slope = slopey / slopex;
                    //}
                    //else
                    //{
                    //    slope = null;
                    //}

                    var plate_coord_vector = { x: slopex, y: slopey };
                    //console.log("("+x+", "+y+") <"+plate_coord_vector.x+", "+plate_coord_vector.y+">");
                    var relmotion = { x: attribute_array[x][y].vector.xcomp - conditions[i].vector.xcomp, y: attribute_array[x][y].vector.ycomp - conditions[i].vector.ycomp };
                    //var relmotion = { x: conditions[i].vector.xcomp - attribute_array[x][y].vector.xcomp, y: conditions[i].vector.ycomp - attribute_array[x][y].vector.ycomp };
                    //console.log(relmotion);

                    //parallel to line between plate coordinates, not parallel to boundary between plates
                    var parallel_component = ((relmotion.x*plate_coord_vector.x) + (relmotion.y*plate_coord_vector.y))/(Math.sqrt(plate_coord_vector.x*plate_coord_vector.x + plate_coord_vector.y*plate_coord_vector.y));                                           
                    var parallel_projection = {x: parallel_component*(plate_coord_vector.x/Math.sqrt(plate_coord_vector.x*plate_coord_vector.x + plate_coord_vector.y*plate_coord_vector.y)), y: parallel_component*(plate_coord_vector.y/Math.sqrt(plate_coord_vector.x*plate_coord_vector.x + plate_coord_vector.y*plate_coord_vector.y))};
                    var perp_projection = { x: relmotion.x - parallel_projection.x, y: relmotion.y - parallel_projection.y };
                    var perp_component = Math.sqrt(perp_projection.x * perp_projection.x + perp_projection.y * perp_projection.y);


                    if(perp_component > Math.abs(parallel_component))
                    {
                        stress_array[x][y] = { isBorder: 1, direct: parallel_component, directvec: parallel_projection, shear: perp_component, shearvec: perp_projection, type: "t", pair_id: {id0: attribute_array[x][y].id, id1: conditions[i].id}, neighbor: {x: neighborx, y: neighbory}, distance: 0 };
                    }
                    else if(parallel_component > 0)
                    {
                        stress_array[x][y] = { isBorder: 1, direct: parallel_component, directvec: parallel_projection, shear: perp_component, shearvec: perp_projection, type: "c", pair_id: {id0: attribute_array[x][y].id, id1: conditions[i].id}, neighbor: {x: neighborx, y: neighbory}, distance: 0 };
                    }
                    else
                    {
                        stress_array[x][y] = { isBorder: 1, direct: parallel_component, directvec: parallel_projection, shear: perp_component, shearvec: perp_projection, type: "d", pair_id: {id0: attribute_array[x][y].id, id1: conditions[i].id}, neighbor: {x: neighborx, y: neighbory}, distance: 0 };
                    }
                    //stress_array[x][y] = { isBorder: 1, direct: parallel_component, directvec: parallel_projection, shear: perp_component, shearvec: perp_projection };


                    if(parallel_component < lowpar)
                    {
                        lowpar = parallel_component;
                    }
                    else if(parallel_component > highpar)
                    {
                        highpar = parallel_component;
                    }
                    if(perp_component < lowperp)
                    {
                        lowperp = perp_component;
                    }
                    else if(perp_component > highperp)
                    {
                        highperp = perp_component;
                    }

                    break;
                }
                else
                {   
                    //create array that only contains border points, with properties for x and y coords of that point, and continent id
                    //calculate distance between (x,y) and border points that have the same continent id as (x,y)
                    
                    stress_array[x][y] = { isBorder: 0, direct: 0, directvec: 0, shear: 0, shearvec: 0, type: "none", pair_id: {id0: attribute_array[x][y].id, id1: null}, neighbor: {x: null, y: null}, distance: 0 };
                }
                
            }

            if(stress_array[x][y].isBorder == 0)
            {
                






            }
            

            if(x == 0)
            {
                postMessage({ 'isData': false, 'setup': true, 'percent': (14/22) + (y * width + x) / (width * height) * (3 / 22) });
            }

        }
    }
    //console.log("Par: <" + lowpar + ", " + highpar + "> Perp: <" + lowperp + ", " + highperp + ">");

    //make all plate boundaries have same stress

    var stress_diff_array = [];

    for (y = 0; y < height; y++ )
    {
        for(x = 0; x < width; x++)
        {
            
            if(stress_array[x][y].isBorder == 1 && stress_array[x][y].direct != stress_array[stress_array[x][y].neighbor.x][stress_array[x][y].neighbor.y].direct)
            {
              //stress_array[stress_array[x][y].neighbor.x][stress_array[x][y].neighbor.y].direct = stress_array[x][y].isBorder == 1 && stress_array[x][y].direct;  
              //stress_diff_array.push()  


            }


        }
    }




        return stress_array;


}




function modifyElevation_worker(elevation_array, stress_array, attribute_array, neighbor_array, width, height, RockMap, OreMap)
{

    //console.log(elevation_array);
    //console.log(attribute_array);

    //postMessage("This is a test");
    Math.seedrandom('', { state: savedmrandstate });
    //console.log("IN FUNCTION RAND: " + Math.random());


    var modifiedElevation = matrix(width, height, 0);
    var modifiedPressure = matrix(width, height, 0);
    var edgeonlyarray = [];
    
    var distance = 0;       
    
    //var pn = new Perlin(PerlinSeeds[3]);
    var pn = new Perlin(Math.random());
    var nx, ny, nz = 0;
    var value = 0;

    var rockpn = new Perlin(Math.random());
    var rnx, rny, rnz = 0;
    var rockval = 0;

    var orepn = new Perlin(Math.random());
    var onx, ony, onz = 0;
    var oreval = 0;
    var oreval2 = 0;
    
    //var ring_count = 1;
    
    //var wrapp = 0;
    //var wrapq = 0;

    var neighborlist = [];

    var pressure_array = [];
    var edgeonlyarray = [];
    var distance_array = [];
    
    var total_pressure = 0;
    var total_distance = 0;

    //var neighbordexstart = 0;
    //var neighbordexend = 0;

    var last_id = 0;


    var disttotal = 0;

    var shortdisttracker = Infinity;
    //var count = 0;

    var distance_factor = 0;
    var dx = 0;

    var best_distance = Infinity;
    var neighborx = 0;
    var neighbory = 0;

    var dist_proportion = 0;
    var distance_total = 0;

    var modifiedbaseel = 0; 
    //go through each pixel
    
    for (var i = 0; i < width; i++ )            //creating an array of edges only. Convert later to find nearest edge by increasing radius instead
    {
        for(var j = 0; j < height; j++)
        {
            if(stress_array[i][j].isBorder == 1)
            {
                edgeonlyarray.push({ x: i, y: j, id: stress_array[i][j].pair_id.id0, neighbor_id: stress_array[i][j].pair_id.id1, type: stress_array[i][j].type, isOceanic: attribute_array[i][j].isOceanic });

            }
        }
    }

    edgeonlyarray.sort(function (a, b) { return a.id - b.id || a.neighbor_id - b.neighbor_id;});

    //console.log(edgeonlyarray);
    //var wrapcount = 0;
    //var notwrapcount = 0;

    //var gradient_init_value = Math.random() * 1.75 - .5;
    //var gradient_init_value = -.3;
    var gradient_init_value = Math.random();

    //if(sea_level <= .25)
    //{
    //    gradient_init_value = Math.random() * 1.25;
    //}
    var gradient_coefficient = Math.random() * .1 + .1;


    //console.log("G_I_V");
    //console.log(gradient_init_value);
    var progresspercent = 0;

    var lowrvalue = Infinity;
    var highrvalue = -Infinity;

    var lowovalue = Infinity;
    var highovalue = -Infinity;

    var lowovalue2 = Infinity;
    var highovalue2 = -Infinity;

    var timer;
    for (var y = 0; y < height; y++)
    {
        for (var x = 0; x < width; x++)
        {
           
            //if (elevation_array[x][y] * attribute_array[x][y].baseEl >= .35)
            //{
                //timer = setTimeout(elevationLoop(-1,-1), 0);
                //progresspercent += 1/(height*width);
                //setTimeout(function(){drawLoadBar(2 / 9 + progresspercent/9);}, 50);



                

                //elevationLoop(x,y);
                //function elevationLoop(x,y){
                pressure_array = [];
                distance_array = [];
                total_distance = 0;
                total_pressure = 0;
                last_id = 0;

                /*if(x == (width-1) && y == (height-1))
                {
                    stop();
                }

                x++;
                x = x % width;

                if(x % width == 0)
                {
                    y++;
                }*/



                neighborlist = neighbor_array.slice(neighbor_array.map(function (e) { return e.id; }).indexOf(stress_array[x][y].pair_id.id0), neighbor_array.map(function (e) { return e.id; }).lastIndexOf(stress_array[x][y].pair_id.id0) + 1);


                for (var j = 0; j < neighborlist.length; j++)
                {
                    distance_array.push(Infinity);
                    //console.log(distance_array);
                }
                //console.log(distance_array);
                //use with for loop going through neighborlist instead of edgeonlyarray

                for (var a = 0; a < neighborlist.length; a++)
                {
                    //console.log(a);
                    //neighbordexstart = edgeonlyarray.findIndex(x => x.id==neighborlist[a].neighbor);
                    //console.log(neighbordexstart);
                    //neighbordexstart = edgeonlyarray.map(function (e) { return e.id; }).indexOf(neighborlist[a].neighbor);          //find start and end indices for that particular neighbor pair, so you only have to go through part of edgeonlyarray
                    //neighbordexend = edgeonlyarray.map(function (e) { return e.id; }).lastIndexOf(neighborlist[a].neighbor);
                    /*if(neighborlist[a].type == "t")
                    {   
                        distance_array[a] = null;
                        pressure_array[a] = { id: neighborlist[a].id, neighbor: neighborlist[a].neighbor, neighborx: null, neighbory: null, direct_force: neighborlist[a].direct_force /*stress_array[edgeonlyarray[n].x][edgeonlyarray[n].y].direct*///, closest_distance: null, type: null };
                    //    continue;
                    //}

                    for(var n = 0; n < edgeonlyarray.length; n++)
                    {
                        //console.log("xy: "+x+" "+y+" a " +a+ " n "+n+" last_id" + last_id);
                        //console.log(n);
                        //console.log(edgeonlyarray[n].id + " / " +edgeonlyarray.length);
                        //console.log("NL Direct: " + neighborlist[a].direct_force + " SA Direct: " + stress_array[edgeonlyarray[n].x][edgeonlyarray[n].y].direct);
                        if (edgeonlyarray[n].id == neighborlist[a].neighbor) //&& attribute_array[edgeonlyarray[n].x][edgeonlyarray[a].y].baseEl * elevation_array[edgeonlyarray[n].x][edgeonlyarray[n].y] >= .35)
                        {   

                            //console.log("NL Direct: " + neighborlist[a].direct_force + " SA Direct: " + stress_array[edgeonlyarray[n].x][edgeonlyarray[n].y].direct);
                            //console.log("in if");

                            //NEED TO CHECK FOR WRAP WHEN CALCULATING DISTANCE - FIND DIST NORMALLY AND WITH WRAP, SEE WHICH IS SMALLER
                            //var wrapdebug = edgeonlyarray[n].x + width - x;
                            //console.log("WrapDebug " + wrapdebug);
                            //console.log(("w: " + edgeonlyarray[n].x + width - x) + "nw: " + (edgeonlyarray[n].x - x));
                            //var xwrapcheck = 0;
                            dx = Math.abs(edgeonlyarray[n].x - x);
                            if(dx > width/2){
                            
                            //if((edgeonlyarray[n].x + width - x) < (Math.abs(edgeonlyarray[n].x - x)))  //Check for Wrap
                            //{
                                xwrapcheck = width - dx;
                                //wrapcount++;
                                //console.log("Wrap"); 
                                 
                            }
                            else
                            {
                                xwrapcheck = dx;
                                //notwrapcount++;
                                //console.log("No wrap");
                            }
                            
                                               
                            distance = Math.sqrt(((xwrapcheck) * (xwrapcheck)) + ((edgeonlyarray[n].y - y) * (edgeonlyarray[n].y - y)));

                            if (distance < distance_array[a])
                            {
                                distance_array[a] = distance;
                                pressure_array[a] = { id: neighborlist[a].id, neighbor: neighborlist[a].neighbor, neighborx: edgeonlyarray[n].x, neighbory: edgeonlyarray[n].y, direct_force: neighborlist[a].direct_force /*stress_array[edgeonlyarray[n].x][edgeonlyarray[n].y].direct*/, shear_force: neighborlist[a].shear_force, closest_distance: distance, type: edgeonlyarray[n].type };

                            }

                            //var neighbornum = neighborlist[a].neighbor;

                            //if(last_id != neighbornum)
                            //{
                            //    break;
                            //}
                            
                        }
                        if(edgeonlyarray[n].id == (neighborlist[a].neighbor + 1))
                        {
                            break;
                        }

                        
                            //last_id = neighbornum;
                        

                    }

                }
                //for (var a = 0; a < edgeonlyarray.length; a++)
                //{

                    //if (a % 100 == 0)
                    //{
                    //console.log(edgeonlyarray[a]);
                    //}
                    /*neighborlist.some(function (entry, n)
                    {
                    if (entry.neighbor == edgeonlyarray[a].id)
                    {
                    neighbordex = n;
                    return true;
                    }
                    });*/

                    /*
                    neighbordex = neighborlist.map(function (e) { return e.neighbor; }).indexOf(edgeonlyarray[a].id);
                    if(neighbordex > -1)
                    {
                    var distance = Math.sqrt((edgeonlyarray[a].x - x)*(edgeonlyarray[a].x - x) + (edgeonlyarray[a].y - y)*(edgeonlyarray[a].y - y));

                    if (distance < distance_array[neighbordex])
                    {
                    distance_array[neighbordex] = distance;
                    pressure_array[neighbordex] = { direct_force: neighborlist[neighbordex].direct_force, closest_distance: distance };


                    }
                    
                    //pressure_array.push({direct_force: neighborlist[neighbordex].direct_force, closest_distance: distance});
                    //neighborlist.splice(neighbordex, 1);
                    //total_distance += distance;

                    }*/

                    //if(neighborlist.length < 1)
                    //{
                    //console.log("for broken");
                    //  break;
                    //}

                    //count++;

                //}



                //---------------------OLD CODE---------------
                /*for(ring_count = 1; ring_count < 25; ring_count++)
                {

                //for each pixel, search for nearest neighbor edges
                for(var p = (-1*ring_count); p <= ring_count; p++)
                {
                for(var q = (-1*ring_count); q <= ring_count; q++)
                {
                wrapp = x + p;
                wrapq = y + q;


                if (wrapp < 0)
                {
                //wrapxl = width - x;
                wrapp = width + wrapp;
                }
                if (wrapp >= width)
                {
                wrapp = wrapp % width;
                //wrapxr = x % width - 1;
                }
                if (wrapq < 0)                   //y doesn't wrap, so don't sample values on other side of image
                {
                continue;
                //wrapq = 0;
                //wrapyu = height - y;
                }
                if (wrapq >= height)
                {
                continue;
                //wrapq = height - 1;
                //wrapyb = y % height - 1;
                }

                if (Math.abs(p) == ring_count || Math.abs(q) == ring_count) //make sure the points are only in the "ring" not in the filled circle
                {
                var neighbordex = neighborlist.map(function (e) { return e.neighbor; }).indexOf(stress_array[wrapp][wrapq].pair_id.id0);
                if(neighbordex > -1)
                {
                var distance = Math.sqrt((wrapp - x)*(wrapp - x) + (wrapq - y)*(wrapq - y));
                                      
                pressure_array.push({direct_force: neighborlist[neighbordex].direct_force, closest_distance: distance});
                neighborlist.splice(neighbordex, 1);
                total_distance += distance;

                }



                }

                }
                }   
                if(neighborlist.length < 1)
                {
                break;
                }
                }*/ // -----------------------END OLD CODE---------------


                //console.log(distance_array);

                //function getSum(total, num) { return total + num;}
                //total_distance = distance_array.reduce(getSum, 0);
                best_distance = Infinity;
                
                //--------------rock variables-------------------
                var best_dpressure = 0;
                var best_spressure = 0;
                var best_type = "";

                var total_shear = 0;
                //-----------------------------------------------
                

                //neighborx = 0;
                //neighbory = 0;
                //console.log("entering loop");
                distance_total = 0;
                
                for (var b = 0; b < pressure_array.length; b++)
                {
                
                        //if(pressure_array[b].closest_distance == null)
                        //{
                        //    continue;
                        //}


                        //if (x < 600 && y < 300)
                        //{
                        //    console.log("(x,y): (" + x + ", " + y + ") ID " + pressure_array[b].id + " NIGHBOR " + pressure_array[b].neighbor + " Distance: " + pressure_array[b].closest_distance + " BestDistance: " + best_distance);
                        //}
                    //console.log("ID " + pressure_array[b].id + " NIGHBOR " + pressure_array[b].neighbor);
                    if(pressure_array[b].closest_distance < best_distance)
                    {   

                        //if (x < 600 && y < 300)
                        //{
                        //    console.log("(x,y): (" + x + ", " + y + ") ID " + pressure_array[b].id + " NIGHBOR " + pressure_array[b].neighbor + " Distance: " + pressure_array[b].closest_distance + " BestDistance: " + best_distance);
                        //}
                        //console.log("(x,y): ("+x+", "+y+") ID " + pressure_array[b].id + " NIGHBOR " + pressure_array[b].neighbor + " Distance: "+pressure_array[b].closest_distance+" BestDistance: "+best_distance);
                        best_distance = pressure_array[b].closest_distance;
                        //neighborx = pressure_array[b].neighborx;
                        //neighbory = pressure_array[b].neighbory;
                        //distance_factor = .4 / (.005 * (pressure_array[b].closest_distance * pressure_array[b].closest_distance) + 1);
                        //total_pressure = pressure_array[b].direct_force; // * distance_factor;

                        best_dpressure = pressure_array[b].direct_force;
                        best_spressure = pressure_array[b].shear_force;
                    
                    }
                   // dist_proportion = 1 - (pressure_array[b].closest_distance / total_distance);
                
                    //console.log("dist prop: " + dist_proportion);
                
                
                
                    //distance_factor = .4 / (.005 * (pressure_array[b].closest_distance * pressure_array[b].closest_distance) + 1);
                    //balance_factor = 1.2 / (1 + (Math.pow(100, (-1*.3 * pressure_array[b].direct_force)))) - .6;


                    if (pressure_array[b].type != "t")
                    {   
                        distance_factor = .4 / (.02 * (pressure_array[b].closest_distance * pressure_array[b].closest_distance) + 1);
                        //total_pressure += (pressure_array[b].direct_force * distance_factor);
                        //total_pressure += distance_factor; //balance_factor; //* distance_factor;
                    }
                    else
                    {   
                        distance_factor = .2 / (.002 * (pressure_array[b].closest_distance * pressure_array[b].closest_distance) + 1);
                        //total_pressure += pressure_array[b].direct_force*distance_factor;
                    }

                    total_pressure += pressure_array[b].direct_force*distance_factor;
                    distance_total += distance_factor;// * pressure_array[b].direct_force;
                    //total_pressure += (pressure_array[b].direct_force * dist_proportion);
                    //console.log("bd in loop: "+best_distance);
                    //disttotal += (best_distance/25);

                    total_shear += pressure_array[b].shear_force*distance_factor;

                    if(best_distance == pressure_array[b].closest_distance)
                    {
                       best_type = pressure_array[b].type;   
                    }



                }

                //-----------------Rock calculations-------------------------
                var igneous_chance = 0;
                var metamorphic_chance = 0;

                rny = 4*y / height;
                        
                rnx = Math.cos((x * 2 * Math.PI) / width); 
                rnz = Math.sin((x * 2 * Math.PI) / width);

                        //Create Elevation Noise
                        //var value = pn.noise(nx, ny, 0) + .5 * pn.noise(2 * nx, 2 * ny, 0) + .25 * pn.noise(4 * nx, 4 * ny, 0) + .125 * pn.noise(8 * nx, 8 * ny, 0) + .0625 * pn.noise(16 * nx, 16 * ny, 0);
                        //var value = pn.noise(nx, ny, nz) + .5 * pn.noise(2 * nx, 2 * ny, 2*nz) + .25 * pn.noise(4 * nx, 4 * ny, 4*nz) + .125 * pn.noise(8 * nx, 8 * ny, 8*nz) + .0625 * pn.noise(16 * nx, 16 * ny, 16*nz);
                rockvalue = rockpn.noise(rnx, rny, rnz) + .5 * rockpn.noise(2*rnx, 2*rny, 2*rnz) + .25 * rockpn.noise(4*rnx, 4*rny, 4*rnz) + .125 * rockpn.noise(8 * nx, 8 * ny, 8 * nz);
                rockvalue *= .7;
                rockvalue = Math.pow(rockvalue, 2);
                //rockvalue *= 1 / (1 + (Math.pow(100, (-1 * 5 * (value - .8)))));


                if (best_type == "c")
                {
                    igneous_chance = 1 / (.00001 * best_distance * best_distance * best_distance + 1);
                }
                else
                {
                    igneous_chance = 1 / (.00005 * best_distance * best_distance * best_distance + 1);
                }

                igneous_chance += rockvalue * 4 - 2;
                //console.log("igneous chance " + igneous_chance);

                //console.log("total press + total shear / 2");
                //console.log(((Math.abs(total_pressure) + Math.abs(total_shear)) / 2) + (rockvalue*1 -.5));
                //console.log("rvalue " + rockvalue);
                if(best_type == "t")
                {
                    //metamorphic_chance = .
                    if(((Math.abs(total_pressure) + Math.abs(total_shear))/2) + (rockvalue*2 -1) >= .09)
                    {

                        //if (Math.random() < .6)
                        //{

                            RockMap[x][y] = "metamorphic";
                        //}
                    }


                }
                else
                {
                    if(((Math.abs(total_pressure) + Math.abs(total_shear))/2) + (rockvalue*2 - 1) >= .1)
                    {
                        //if (Math.random() < .5)
                        //{
                            RockMap[x][y] = "metamorphic";
                        //}
                    }
                }


                if (RockMap[x][y] != "metamorphic")
                {


                    if(rockvalue > .4 || igneous_chance > .25)
                    {
                        RockMap[x][y] = "igneous";
                    }

                    /*if(igneous_chance > .25)
                    {

                        RockMap[x][y] = "igneous";
                    }*/

                    //if (Math.random() < igneous_chance)
                    //{
                    //    RockMap[x][y] = "igneous";
                    //}
                    else
                    {
                        RockMap[x][y] = "sedimentary";
                    }
                }

                if(rockvalue < lowrvalue)
                {
                    lowrvalue = rockvalue;
                }
                if(rockvalue > highrvalue)
                {
                    highrvalue = rockvalue;
                }

                //-------------------------------------------------------------

                //------------------Ore Calc-----------------------------------


                ony = 4*y / height;
                        
                onx = Math.cos((x * 2 * Math.PI) / width); 
                onz = Math.sin((x * 2 * Math.PI) / width);

                orevalue = /*orepn.noise(onx, ony, onz) + .5 * orepn.noise(2*onx, 2*ony, 2*onz) + */.25 * orepn.noise(4*onx, 4*ony, 4*onz) + .125 * orepn.noise(8 * onx, 8 * ony, 8 * onz) + .0625 * orepn.noise(16*onx, 16*ony, 16*onz);
                //orevalue = orepn.noise(onx, ony, onz) + .5 * orepn.noise(2*onx, 2*ony, 2*onz) + .25 * orepn.noise(4*onx, 4*ony, 4*onz) + .125 * orepn.noise(8 * onx, 8 * ony, 8 * onz) + .0625 * orepn.noise(16*onx, 16*ony, 16*onz);
                //orevalue *= .7;
                //orevalue = Math.pow(orevalue, 2);

                //orevalue += (Math.random() * .3 - .15);
                //orevalue += (Math.random() * .1 - .05);
                //orevalue += (Math.random() * .04 - .02);
                //orevalue = (orevalue + rockvalue) / 2;
                //orevalue +=  rockvalue*.3 - .15;
                //orevalue = (orevalue + (rockvalue*.7 - .35)) / 2;
                //orevalue = (orevalue * (rockvalue*.7 - .35));
                orevalue = ((orevalue - .07) / .3);
                orevalue2 = (orevalue + rockvalue) / 2;


                if(orevalue < .325 || orevalue > .675)
                {
                    if(RockMap[x][y] == "sedimentary")
                    {
                    
                        if(orevalue2 < .28)
                        {
                            OreMap[x][y] = "coal";
                        }
                        else if(orevalue2 < .35 && orevalue2 >= .28)
                        {
                            OreMap[x][y] = "copper";
                        }    
                        else if(orevalue2 < .45 && orevalue2 >= .35)
                        {
                            OreMap[x][y] = "tin";
                        }
                        else if(orevalue2 < .5 && orevalue2 >= .45)
                        {
                            OreMap[x][y] = "iron";
                        }
                        else if(orevalue2 < .58 && orevalue2 >= .5)
                        {
                            OreMap[x][y] = "gold";
                        }
                        else if(orevalue2 >= .58)//< .7)
                        {
                            OreMap[x][y] = "diamond";
                        }
                        else
                        {
                            OreMap[x][y] = "none";
                        }
                    }        
                    else if(RockMap[x][y] == "igneous")
                    {

                        if(orevalue2 < .22)
                        {
                            OreMap[x][y] = "copper";
                        }
                        else if(orevalue2 < .28 && orevalue2 >= .22)
                        {
                            OreMap[x][y] = "platinum";
                        }    
                        else if(orevalue2 < .37 && orevalue2 >= .28)
                        {
                            OreMap[x][y] = "aluminum";
                        }
                        else if(orevalue2 < .46 && orevalue2 >= .37)
                        {
                            OreMap[x][y] = "iron";
                        }
                        else if(orevalue2 < .55 && orevalue2 >= .46)
                        {
                            OreMap[x][y] = "silver";
                        }
                        else if(orevalue2 < .62 && orevalue2 >= .55)
                        {
                            OreMap[x][y] = "tin";
                        }
                        else if(orevalue2 >= .62)//< .7)
                        {
                            OreMap[x][y] = "diamond";
                        }
                        else
                        {
                            OreMap[x][y] = "none";
                        }
                    }    
                    else
                    {
                        if(orevalue2 < .28)
                        {
                            OreMap[x][y] = "copper";
                        }
                        else if(orevalue2 < .36 && orevalue2 >= .28)
                        {
                            OreMap[x][y] = "lead";
                        }    
                        else if(orevalue2 < .51 && orevalue2 >= .36)
                        {
                            OreMap[x][y] = "silver";
                        }
                        else if(orevalue2 < .62 && orevalue2 >= .51)
                        {
                            OreMap[x][y] = "gold";
                        }
                        else if(orevalue2 >= .67)
                        {
                            OreMap[x][y] = "diamond";
                        }
                        else
                        {
                            OreMap[x][y] = "none";
                        }
                    }




                }
                else
                {
                  OreMap[x][y] = "none";  
                }



                /*if(orevalue < .33)
                {
                    OreMap[x][y] = "copper";
                }
                else if(orevalue >= .33 && orevalue < .66)
                {
                    OreMap[x][y] = "coal";
                }
                else if(orevalue >= .66)
                {
                    OreMap[x][y] = "gold";   
                }*/
                /*if(RockMap[x][y] == "sedimentary")
                {
                    console.log("Sedimentary");
                    console.log("Orevalue: " + orevalue);
                    

                    //if(orevalue < .3)
                    //{
                    //    OreMap[x][y] = "none";
                    //}
                    if(orevalue < .2)
                    {
                        OreMap[x][y] = "copper";
                    }
                    else if(orevalue < .35 && orevalue > .32)
                    {
                        OreMap[x][y] = "diamond";
                    }
                    else if(orevalue < .43 && orevalue > .39)
                    {
                        OreMap[x][y] = "iron";
                    }
                    else if(orevalue < .5 && orevalue > .45)
                    {
                        OreMap[x][y] = "tin";
                    }
                    else if(orevalue < .585 && orevalue > .55)
                    {
                        OreMap[x][y] = "gold";
                    }
                    else if(orevalue < .67 && orevalue > .6)
                    {
                        OreMap[x][y] = "coal";
                    }
                    else
                    {
                        OreMap[x][y] = "none";
                    }
                    
                        
                }
                else if(RockMap[x][y] == "igneous")
                {
                    console.log("Igneous");
                    console.log("Orevalue: " + orevalue);
                    

                    //if(orevalue < .3 && orevalue > .25)
                    //{
                    //    OreMap[x][y] = "none";
                    //}
                    if(orevalue < .25 && orevalue > .2)
                    {
                        OreMap[x][y] = "copper";
                    }
                    else if(orevalue < .27 && orevalue > .24)
                    {
                        OreMap[x][y] = "diamond";
                    }
                    else if(orevalue < .35 && orevalue > .3)
                    {
                        OreMap[x][y] = "iron";
                    }
                    else if(orevalue < .42 && orevalue > .37)
                    {
                        OreMap[x][y] = "tin";
                    }
                    else if(orevalue < .49 && orevalue > .53)
                    {
                        OreMap[x][y] = "silver";
                    }
                    else if(orevalue < .58 && orevalue > .55)
                    {
                        OreMap[x][y] = "platinum";
                    }
                    else if(orevalue < .65 && orevalue > .6)
                    {
                        OreMap[x][y] = "aluminum";
                    }
                    else
                    {
                        OreMap[x][y] = "none";
                    }
                }
                else
                {
                    console.log("Metamorphic");
                    console.log("Orevalue: " + orevalue);
                    
                    
                    //if(orevalue < .3)
                    //{
                    //    OreMap[x][y] = "none";
                    //}
                    if(orevalue < .25 && orevalue > .2)
                    {
                        OreMap[x][y] = "copper";
                    }
                    else if(orevalue < .32 && orevalue > .27)
                    {
                        OreMap[x][y] = "lead";
                    }
                    else if(orevalue < .39 && orevalue > .35)
                    {
                        OreMap[x][y] = "silver";
                    }
                    else if(orevalue < .455 && orevalue > .42)
                    {
                        OreMap[x][y] = "gold";
                    }
                    else
                    {
                        OreMap[x][y] = "none";
                    }
                    
                        
                }*/

                //console.log(OreMap[x][y]);



                //orevalue = ((orevalue - .05) / .3);
                //OreMap[x][y] = orevalue;
                


                if(orevalue < lowovalue)
                {
                    lowovalue = orevalue;
                }
                if(orevalue > highovalue)
                {
                    highovalue = orevalue;
                }

                 if(orevalue2 < lowovalue2)
                {
                    lowovalue2 = orevalue2;
                }
                if(orevalue2 > highovalue2)
                {
                    highovalue2 = orevalue2;
                }






                //total_pressure /= pressure_array.length;
                //console.log("bd after loop: "+best_distance);
                //best_distance /= 25; //possibly modify best distance???-----------

                //if(best_distance < shortdisttracker)
                //{
                //    shortdisttracker = best_distance;
                //}

                //modifiedPressure[x][y] = total_pressure;
                //console.log("TP " + total_pressure);

                modifiedbaseel = attribute_array[x][y].baseEl + ((1 / (.01 * (best_distance*best_distance) + 1)) * (1 - attribute_array[x][y].baseEl));

                //console.log(total_pressure);
                //modifiedElevation[x][y] += (total_pressure + elevation_array[x][y]);
                

                //modifiedElevation[x][y] = .5 + total_pressure;
                //modifiedElevation[x][y] = distance_total;
                //console.log("DT " + distance_total);



                ny = 4*y / height;
                        
                nx = Math.cos((x * 2 * Math.PI) / width); 
                nz = Math.sin((x * 2 * Math.PI) / width);

                        //Create Elevation Noise
                        //var value = pn.noise(nx, ny, 0) + .5 * pn.noise(2 * nx, 2 * ny, 0) + .25 * pn.noise(4 * nx, 4 * ny, 0) + .125 * pn.noise(8 * nx, 8 * ny, 0) + .0625 * pn.noise(16 * nx, 16 * ny, 0);
                        //var value = pn.noise(nx, ny, nz) + .5 * pn.noise(2 * nx, 2 * ny, 2*nz) + .25 * pn.noise(4 * nx, 4 * ny, 4*nz) + .125 * pn.noise(8 * nx, 8 * ny, 8*nz) + .0625 * pn.noise(16 * nx, 16 * ny, 16*nz);
                value = .125 * pn.noise(8 * nx, 8 * ny, 8*nz) + .0625 * pn.noise(16 * nx, 16 * ny, 16*nz) + .03125 * pn.noise(32 * nx, 32 * ny, 32 * nz);
                value *= 7;
                value *= 1 / (1 + (Math.pow(100, (-1 * 5 * (value - .8)))));

                //var gradient_value = pn.noise(nx, ny, nz) + .5 * pn.noise(2 * nx, 2 * ny, 2*nz) + .25 * pn.noise(4 * nx, 4 * ny, 4*nz) + .125 * pn.noise(8 * nx, 8 * ny, 8*nz) + .0625 * pn.noise(16 * nx, 16 * ny, 16*nz);
                //gradient_value /= 1.28;
                //gradient_value = Math.pow(gradient_value, 2);
                           
                //value *= 1 / (1 + (Math.pow(100, (-1*5 * (value - .1)))));
                //total_pressure *= value;
                //var gradient_init_value = Math.random() * 1.75 - .5;
                var gradient_factor = (y / (gradient_coefficient /*.1*/ * height)) + gradient_init_value; //(y / (height / 10));
                
                if(y >= (height * ( gradient_coefficient /*.1*/ * gradient_init_value + (1 - gradient_coefficient)/*.9*/)))
                {
                    gradient_factor = -1 * (1 / (gradient_coefficient /*.1*/ * height)) * (y - ((1 - gradient_coefficient) /*.9*/ * height)) + 1 + gradient_init_value;
                    //if (x == 0)
                    //{
                    //    console.log("y: " + y + " gf: " + gradient_factor);
                    //}
                    //gradient_factor *= gradient_value;
                }
                //if (y <= .2 * height)
                //{
                    //gradient_factor *= gradient_value;
                //}
                
                //if(y <= .15* height || y >= .85*height)
                //{
                //    gradient_factor *= gradient_value;
                //}
                if(gradient_factor > 1)
                {
                    gradient_factor = 1;
                }


                //drawArray(x, y, gradient_factor, "myCanvas12");

                if(((elevation_array[x][y] + (total_pressure*.7*value)) * (attribute_array[x][y].baseEl) /** gradient_factor*/) >= sea_level)//if (elevation_array[x][y] * attribute_array[x][y].baseEl >= .35) //&& attribute_array[neighborx][neighbory].baseEl * elevation_array[neighborx][neighbory] >= .35) //uncomment here
                {
                    //modifiedElevation[x][y] = elevation_array[x][y];
                    //modifiedElevation[x][y] = total_pressure;
                    modifiedElevation[x][y] += ((elevation_array[x][y]+(total_pressure*value)) * modifiedbaseel * gradient_factor);
                    modifiedElevation[x][y] = modifiedElevation[x][y] + (.15 * (1 - modifiedElevation[x][y])) - .12;
                }
                else
                {
                    modifiedElevation[x][y] += ((elevation_array[x][y] + (total_pressure*.7*value)) * (attribute_array[x][y].baseEl*modifiedbaseel));
                    //modifiedElevation[x][y] = ((elevation_array[x][y] + total_pressure) * .3*modifiedbaseel);//attribute_array[x][y].baseEl);
                    //modifiedElevation[x][y] = modifiedElevation[x][y] + (.15 * (1 - modifiedElevation[x][y]));
                }
               
               
                //console.log(total_pressure);
                //modifiedElevation[x][y] *= 2*total_pressure; //try modifying formula for total pressure (get more influence from close by borders
           // }
              //if(stress_array[x][y].isBorder == 1 && stress_array[x][y].direct != stress_array[stress_array[x][y].neighbor.x][stress_array[x][y].neighbor.y].direct){
              //  console.log("SA Direct: "+stress_array[x][y].direct+" SA Neighbor Direct: "+stress_array[stress_array[x][y].neighbor.x][stress_array[x][y].neighbor.y].direct + " Current X/Y: "+x+", "+y+ " Neighbor X/Y: "+stress_array[x][y].neighbor.x+", "+stress_array[x][y].neighbor.y+" Neighbor's Neighbor: "+stress_array[stress_array[x][y].neighbor.x][stress_array[x][y].neighbor.y].neighbor.x+", "+stress_array[stress_array[x][y].neighbor.x][stress_array[x][y].neighbor.y].neighbor.y);
              //}

                //progresspercent += 1/(height*width);
                //drawLoadBar(2 / 9 + (progresspercent/12));


                /*if(x == (width-1) && y == (height-1))
                {
                    stop();
                }

                x++;
                x = x % width;

                if(x % width == 0)
                {
                    y++;
                }*/

                //timer = setTimeout(elevationLoop(x, y), 0);


              //}




              //function stop()
              //{
              //    clearInterval(timer);
              //}
                //progresspercent += 1/(height*width);
                //drawLoadBar(2 / 9 + (progresspercent/12));

                if (x == 0)
                {
                    postMessage({ 'isData': false, 'setup': false, 'x': x, 'y': y });
                }
               
        }
    }
    //console.log("LOW R VALUE " + lowrvalue);
    //console.log("High R VALUE " + highrvalue);
    //console.log("LOW O VALUE " + lowovalue);
    //console.log("High O VALUE " + highovalue);
    //console.log("LOW O2 VALUE " + lowovalue2);
    //console.log("High O2 VALUE " + highovalue2);
    //console.log("WrapCount: "+wrapcount);
    //console.log("NotWrapCount: " + notwrapcount);
    //console.log("WrapDebug " + wrapdebug);
    //TEST
    //for (var k = 0; k < edgeonlyarray.length; k++ )
    //{

    //    modifiedElevation[edgeonlyarray[k].x][edgeonlyarray[k].y] = Math.abs(stress_array[edgeonlyarray[k].x][edgeonlyarray[k].y].direct); 

    //}





        //disttotal /= (width * height);
    //console.log(disttotal);
    //console.log("Shortest Dist: "+shortdisttracker);
    //console.log(count);

   // modifiedElevation = averageArray(modifiedElevation, elevation_array, width, height, 3, false);
    //modifiedPressure = averageArray(modifiedPressure, elevation_array, width, height);

    //for (y = 0; y < height; y++)
    //{
    //    for (x = 0; x < width; x++)
    //    {
    //        modifiedElevation[x][y] = (elevation_array[x][y] + modifiedPressure[x][y]) * modifiedElevation[x][y];
    //    }
   // }
    isElevationComplete = true;
    
    
    //return modifiedElevation;
    //console.log("LAST IN FUNCTION RAND " + Math.random());
    
    postMessage({ 'isData': true, 'setup': false, 'elArr': modifiedElevation, 'rockArr': JSON.stringify(RockMap), 'oreArr': JSON.stringify(OreMap), /*'plate_coords': plate_coords, /*'plate_coords_wrapped': plate_coords_wrapped, 'vor_result': vor_result, 'vor_result_wrapped': vor_result_wrapped,*/
    /*'PlateIdentifierArrayWrap': PlateIdentifierArrayWrap, 'PlateIdentifierArrayWrapCrop': PlateIdentifierArrayWrapCrop,*/
    /*'PlateTurbArray2': PlateTurbArray2, /*'PlateEdgeArray': PlateEdgeArray,*/ /*'PlateBoundaryStressArray': PlateBoundaryStressArray, 'PlateAttributeArray': PlateAttributeArray, 'plate_neighbors': plate_neighbors, /*'PerlinElevationArray': PerlinElevationArray,*/ 'savedRand': Math.random.state()});
    
    
    //return modifiedPressure;    

}

