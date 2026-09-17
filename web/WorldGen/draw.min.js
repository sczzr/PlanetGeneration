

function drawArray(x, y, array_val, canvas_id){

        //imgLegend.src = "generator_legends_multi_elevation.png";
        
        
        var c = document.getElementById(canvas_id);
        var ctx = c.getContext("2d");
        //var leg = document.getElementById("legendCanvas");
        //var legctx = leg.getContext("2d");

        //legctx.drawImage(imgLegendElevation, 0, 0);

        ctx.fillStyle = colorLuminance("#FFFFFF", array_val);
        ctx.fillRect(x, y, 1, 1);
}

function drawElevationArray(x, y, array_val, canvas_id){
        
        //imgLegend.src = "generator_legends_multi_elevation.png";
        

        var c = document.getElementById(canvas_id);
        var ctx = c.getContext("2d");
        //var leg = document.getElementById("legendCanvas");
        //var legctx = leg.getContext("2d");

        //legctx.drawImage(imgLegendElevation, 0, 0);

        ctx.fillStyle = colorLuminance("#FFFFFF", array_val);
        if(array_val < .5714*sea_level)//.2)
        {
            //ctx.fillStyle = "#16789c";
            ctx.fillStyle = "#1a2482";  //#0099cc

        }
        else if(array_val < .8 * sea_level)//.28)
        {
            //ctx.fillStyle = "#0099cc";
            ctx.fillStyle = "#0059b3";  //#33E3FF
        }
        else if(array_val < sea_level)               //draw ocean
        {
            ctx.fillStyle = "#0059b3";
            //ctx.fillStyle = "#0099cc";
        }
        ctx.fillRect(x, y, 1, 1);
}

function drawRiverLayer(x, xt, y, yt, canvas_id)
{

    var c = document.getElementById(canvas_id);
    var ctx = c.getContext("2d");
    
    if (RiverLayer[x][y] > 0){ //&& ModifiedElevationArray[x][y] >= sea_level){

       ctx.fillStyle = "blue";
       ctx.fillRect(xt, yt, 1, 1);
    }

}


function drawLandOutline(x, xt, y, yt, canvas_id)
{

    var c = document.getElementById(canvas_id);
    var ctx = c.getContext("2d");
    
    if (ModifiedElevationArray[x][y] >= sea_level){

        ctx.fillStyle = "white";
    }
    else{
        ctx.fillStyle = "black";
    }

    ctx.fillRect(xt, yt, 1, 1);

}


function drawTemperatureArray(x, xt, y, yt, array_val, canvas_id){
        
        //imgLegend.src = "generator_legends_multi_temp.png";

        var c = document.getElementById(canvas_id);
        var ctx = c.getContext("2d");
        //var leg = document.getElementById("legendCanvas");
        //var legctx = leg.getContext("2d");

        //legctx.drawImage(imgLegendTemp, 0, 0);
        //var chosenColor = "";

        //if(ModifiedElevationArray[x][y] < sea_level)
        //{
        //    array_val *= .75;
        //}

        ctx.fillStyle = colorLuminance("#FFFFFF", array_val);
        if(array_val < .05)
        {
            ctx.fillStyle = "#0066ff";
            //chosenColor = "#0066ff";
        }
        else if(array_val <.18)
        {
            ctx.fillStyle = "#00e6b8";
            //chosenColor = "#00e6b8";
        }
        else if(array_val < .3)               
        {
            ctx.fillStyle = "#66ff99";
            //chosenColor = "#66ff99";
        }
        else if(array_val < .47)               
        {
            ctx.fillStyle = "#ffff99";
            //chosenColor = "#ffff99";
        }
        else if(array_val < .7)               
        {
            ctx.fillStyle = "#ff6600";
            //chosenColor = "#ff6600";
        }
        else
        {
            ctx.fillStyle = "#cc0000";
            //chosenColor = "#cc0000"; 
        }

        var abort = false;
        for (var i = -1; i <= 1 && !abort; i++ )
        {
            for(var j = -1; j <= 1 && !abort; j++)
            {
                if (i == 0 && j == 0) { continue; }
                var xp = (x + i)%width;
                if (xp < 0) { xp = width - 1; }
                var yp = y + j;
                if (yp < 0) { yp = 0; } else if (yp >= height) { yp = height - 1; }
                if (ModifiedElevationArray[x][y] >= sea_level && ModifiedElevationArray[xp][yp] < sea_level)
            {
                ctx.fillStyle = "brown";
                abort = true;

            }   


            }
        }
            
        /*if (ModifiedElevationArray[x][y] < sea_level)
        {   
            
            ctx.fillStyle = colorLuminance(ctx.fillStyle, .1);
        }*/
        
        
            ctx.fillRect(xt, yt, 1, 1);
        
}

function drawMoistureArray(x, y, array_val, canvas_id){
        
        //imgLegend.src = "generator_legends_multi_moisture.png";
        
        var c = document.getElementById(canvas_id);
        var ctx = c.getContext("2d");
        //var leg = document.getElementById("legendCanvas");
        //var legctx = leg.getContext("2d");

        //legctx.drawImage(imgLegendMoisture, 0, 0);



        ctx.fillStyle = colorLuminance("#66ff99", array_val);
        
        ctx.fillRect(x, y, 1, 1);
}

function drawPlateIDArray(x,y,array_val, canvas_id, randColors)
{
        //imgLegend.src = "generator_legends_multi_tecplates.png";
        
        var c = document.getElementById(canvas_id);
        var ctx = c.getContext("2d");
        //var leg = document.getElementById("legendCanvas");
        //var legctx = leg.getContext("2d");

        //legctx.drawImage(imgLegendTecPlates, 0, 0);

        if(array_val < randColors.length){
            ctx.fillStyle = randColors[array_val];

        }
        else
        {
            ctx.fillStyle = "#000000";
            //console.log(x+" "+y);
        }
        ctx.fillRect(x, y, 1, 1);
}

function drawWindArray(wind_array, width, height, canvas_id){
   
    var c = document.getElementById(canvas_id);
    var ctx = c.getContext("2d");


   for(var y = 0; y < height; y++){
       for(var x = 0; x < width; x++){

           var xt = (x + translated) % width;
                        if (translated < 0)
                        {
                            xt = (width + (x - (Math.abs(translated)%width))) % width;
                        }
           
           if(x % 16 == 0 && y % 16 == 0){
           
           var mag = Math.sqrt(wind_array[x][y].xcomp*wind_array[x][y].xcomp + wind_array[x][y].ycomp*wind_array[x][y].ycomp);
           ctx.fillStyle = "#FFFFFF";   
           ctx.beginPath();
           ctx.arc(xt,y,2,0,2*Math.PI);   //x----->xt
           ctx.moveTo(xt,y);
           //if(y == 0)
           //{
           //    console.log("Wind mag for x,y: " + x + ", " + y + " " + mag);
           //}
           if (mag > 20)
           {
               ctx.lineTo(xt + 15*wind_array[x][y].xcomp / mag, y + 15*wind_array[x][y].ycomp / mag);
           }
           else
           {
               ctx.lineTo(xt + wind_array[x][y].xcomp, y + wind_array[x][y].ycomp);
           }
           //ctx.arc(coords_array[i].x,coords_array[i].y,5,0,2*Math.PI);
           ctx.strokeStyle = "#FFFFFF";
           ctx.stroke();

           }
       }
   } 

}

function drawBiomes(x, y, biome, canvas_id){     //, randColors){

    //console.log("reached draw");
    //console.log("Biome: " + biome);

        //imgLegend.src = "generator_legends_multi_biomes.png";

        var c = document.getElementById(canvas_id);
        var ctx = c.getContext("2d");
        //var leg = document.getElementById("legendCanvas");
        //var legctx = leg.getContext("2d");

        //legctx.drawImage(imgLegendBiomes, 0, 0);

        var OCEAN = "#1a2482";              //O = #0059b3/ SO = #33ccff
        var SHALLOW_OCEAN = "#0059b3";      //O = #1a2482/ SO = #0059b3
        var COASTLAND = "#ffffcc";
        var TROPICAL_RAIN_FOREST = "#004800";
        var TROPICAL_SEASONAL_FOREST = "#0c8d0c";
        var SHRUBLAND = "#607818";
        var SAVANNAH = "#f4f48b";
        var TROPICAL_DESERT = "#a86048";
        var TEMPERATE_RAIN_FOREST = "#64b464";    //#64b464 #3d8f3d
        var TEMPERATE_SEASONAL_FOREST = "#628f56";
        var CHAPARRAL = "#8f849a";
        var GRASSLAND = "#90d848";
        var STEPPE = "#bfbfbf";
        var TEMPERATE_DESERT = "#d8a878";
        var BOREAL_FOREST = "#006048";
        var TAIGA = "#489090";
        var TUNDRA = "#8CCCBD";
        var ICE = "#b3ecff";
        var ROCKY_MOUNTAIN = "#ad421f";
        var SNOWY_MOUNTAIN = "#e6f3ff";
        var RIVER = "#0059b3";

        /*if (document.getElementById('RandBiomColor').checked){

            var OCEAN = randColors[0];
            var BEACH = randColors[1];
            var SCORCHED = randColors[2];
            var BARE = randColors[3];
            var TUNDRA = randColors[4];
            var SNOW = randColors[5];
            var TEMPERATE_DESERT = randColors[6];
            var SHRUBLAND = randColors[7];
            var TAIGA = randColors[8];
            var GRASSLAND = randColors[9];
            var TEMPERATE_DECIDUOUS_FOREST = randColors[10];
            var TEMPERATE_RAIN_FOREST = randColors[11];
            var SUBTROPICAL_DESERT = randColors[12];
            var TROPICAL_SEASONAL_FOREST = randColors[13];
            var TROPICAL_RAIN_FOREST = randColors[14];
        }*/

        switch (biome){
            case "OCEAN":
                ctx.fillStyle = OCEAN;
                break;
            case "SHALLOW OCEAN":
                ctx.fillStyle = SHALLOW_OCEAN;
                break;
            case "COASTLAND":
                ctx.fillStyle = COASTLAND;
                break;
            case "TROPICAL RAIN FOREST":
                ctx.fillStyle = TROPICAL_RAIN_FOREST;
                break;
            case "TROPICAL SEASONAL FOREST":
                ctx.fillStyle = TROPICAL_SEASONAL_FOREST;
                break;
            case "SHRUBLAND":
                ctx.fillStyle = SHRUBLAND;
                break;
            case "SAVANNAH":
                ctx.fillStyle = SAVANNAH;
                break;
            case "TROPICAL DESERT":
                ctx.fillStyle = TROPICAL_DESERT;
                break;
            case "TEMPERATE RAIN FOREST":
                ctx.fillStyle = TEMPERATE_RAIN_FOREST;
                break;
            case "TEMPERATE SEASONAL FOREST":
                ctx.fillStyle = TEMPERATE_SEASONAL_FOREST;
                break;
            case "CHAPARRAL":
                ctx.fillStyle = CHAPARRAL;
                break;
            case "GRASSLAND":
                ctx.fillStyle = GRASSLAND;
                break;
            case "STEPPE":
                ctx.fillStyle = STEPPE;
                break;
            case "TEMPERATE DESERT":
                ctx.fillStyle = TEMPERATE_DESERT;
                break;
            case "BOREAL FOREST":
                ctx.fillStyle = BOREAL_FOREST;
                break;
            case "TAIGA":
                ctx.fillStyle = TAIGA;
                break;
            case "TUNDRA":
                ctx.fillStyle = TUNDRA;
                break;
            case "ICE":
                ctx.fillStyle = ICE;
                break;
            case "ROCKY MOUNTAIN":
                ctx.fillStyle = ROCKY_MOUNTAIN;
                break;
            case "SNOWY MOUNTAIN":
                ctx.fillStyle = SNOWY_MOUNTAIN;
                break;
            case "RIVER":
                ctx.fillStyle = RIVER;
                break;
            default:
                ctx.fillStyle = "#000000";
                break;
        }

        ctx.fillRect(x, y, 1, 1);

}

function drawRColorBiomes(){     //, randColors){

    //console.log("reached draw");
    //console.log("Biome: " + biome);
        var biomeColorArray = [];    

        for (var i = 0; i < 21; i++)
                {
                    biomeColorArray[i] = get_random_color();

                }


        var c = document.getElementById("myCanvas11");
        var ctx = c.getContext("2d");

        var OCEAN = biomeColorArray[0];              //O = #0059b3/ SO = #33ccff
        var SHALLOW_OCEAN = biomeColorArray[1];      //O = #1a2482/ SO = #0059b3
        var COASTLAND = biomeColorArray[2];
        var TROPICAL_RAIN_FOREST = biomeColorArray[3];
        var TROPICAL_SEASONAL_FOREST = biomeColorArray[4];
        var SHRUBLAND = biomeColorArray[5];
        var SAVANNAH = biomeColorArray[6];
        var TROPICAL_DESERT = biomeColorArray[7];
        var TEMPERATE_RAIN_FOREST = biomeColorArray[8];    //#64b464 #3d8f3d
        var TEMPERATE_SEASONAL_FOREST = biomeColorArray[9];
        var CHAPARRAL = biomeColorArray[10];
        var GRASSLAND = biomeColorArray[11];
        var STEPPE = biomeColorArray[12];
        var TEMPERATE_DESERT = biomeColorArray[13];
        var BOREAL_FOREST = biomeColorArray[14];
        var TAIGA = biomeColorArray[15];
        var TUNDRA = biomeColorArray[16];
        var ICE = biomeColorArray[17];
        var ROCKY_MOUNTAIN = biomeColorArray[18];
        var SNOWY_MOUNTAIN = biomeColorArray[19];
        var RIVER = biomeColorArray[20];

        /*if (document.getElementById('RandBiomColor').checked){

            var OCEAN = randColors[0];
            var BEACH = randColors[1];
            var SCORCHED = randColors[2];
            var BARE = randColors[3];
            var TUNDRA = randColors[4];
            var SNOW = randColors[5];
            var TEMPERATE_DESERT = randColors[6];
            var SHRUBLAND = randColors[7];
            var TAIGA = randColors[8];
            var GRASSLAND = randColors[9];
            var TEMPERATE_DECIDUOUS_FOREST = randColors[10];
            var TEMPERATE_RAIN_FOREST = randColors[11];
            var SUBTROPICAL_DESERT = randColors[12];
            var TROPICAL_SEASONAL_FOREST = randColors[13];
            var TROPICAL_RAIN_FOREST = randColors[14];
        }*/
        var biome = "";

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {

                var xt = (x + translated) % width;
                        if (translated < 0)
                        {
                           
                            xt = (width + (x - (Math.abs(translated)%width))) % width;
                            
                        }


                biome = BiomeMap[x][y];

                switch (biome)
                {
                    case "OCEAN":
                        ctx.fillStyle = OCEAN;
                        break;
                    case "SHALLOW OCEAN":
                        ctx.fillStyle = SHALLOW_OCEAN;
                        break;
                    case "COASTLAND":
                        ctx.fillStyle = COASTLAND;
                        break;
                    case "TROPICAL RAIN FOREST":
                        ctx.fillStyle = TROPICAL_RAIN_FOREST;
                        break;
                    case "TROPICAL SEASONAL FOREST":
                        ctx.fillStyle = TROPICAL_SEASONAL_FOREST;
                        break;
                    case "SHRUBLAND":
                        ctx.fillStyle = SHRUBLAND;
                        break;
                    case "SAVANNAH":
                        ctx.fillStyle = SAVANNAH;
                        break;
                    case "TROPICAL DESERT":
                        ctx.fillStyle = TROPICAL_DESERT;
                        break;
                    case "TEMPERATE RAIN FOREST":
                        ctx.fillStyle = TEMPERATE_RAIN_FOREST;
                        break;
                    case "TEMPERATE SEASONAL FOREST":
                        ctx.fillStyle = TEMPERATE_SEASONAL_FOREST;
                        break;
                    case "CHAPARRAL":
                        ctx.fillStyle = CHAPARRAL;
                        break;
                    case "GRASSLAND":
                        ctx.fillStyle = GRASSLAND;
                        break;
                    case "STEPPE":
                        ctx.fillStyle = STEPPE;
                        break;
                    case "TEMPERATE DESERT":
                        ctx.fillStyle = TEMPERATE_DESERT;
                        break;
                    case "BOREAL FOREST":
                        ctx.fillStyle = BOREAL_FOREST;
                        break;
                    case "TAIGA":
                        ctx.fillStyle = TAIGA;
                        break;
                    case "TUNDRA":
                        ctx.fillStyle = TUNDRA;
                        break;
                    case "ICE":
                        ctx.fillStyle = ICE;
                        break;
                    case "ROCKY MOUNTAIN":
                        ctx.fillStyle = ROCKY_MOUNTAIN;
                        break;
                    case "SNOWY MOUNTAIN":
                        ctx.fillStyle = SNOWY_MOUNTAIN;
                        break;
                    case "RIVER":
                        ctx.fillStyle = RIVER;
                        break;
                    default:
                        ctx.fillStyle = "#000000";
                        break;
                }

                ctx.fillRect(xt, y, 1, 1);
            }
        }

}

function drawEdgeTypeOverlay(x,y,stress_array, canvas_id)
{
        var c = document.getElementById(canvas_id);
        var ctx = c.getContext("2d");

        if(stress_array.isBorder != 0)
        {
           //ctx.fillStyle = "#000000";
            
       // }
       // else
        //{
           if(stress_array.type == "t")
           {
               //ctx.fillStyle = "white";
               ctx.fillStyle = colorLuminance("#FFFFFF", stress_array.shear);
           } 
           else if(stress_array.type == "c")
           {
               //ctx.fillStyle = "red";
               ctx.fillStyle = colorLuminance("#FF0000", stress_array.direct);
           }
           else
           {
               //ctx.fillStyle = "blue";
               ctx.fillStyle = colorLuminance("#0000FF", Math.abs(stress_array.direct));
           }
           //console.log("("+x+", "+y+") direct: " + stress_array.direct + " directvec: <"+stress_array.directvec.x+", "+stress_array.directvec.y+"> shear: " + stress_array.shear + " shearvec: <"+stress_array.shearvec.x+", "+stress_array.shearvec.y+"> type: " + stress_array.type);
            ctx.fillRect(x, y, 1, 1);
        }    
            
        
        //ctx.fillRect(x, y, 1, 1);
}

function drawEdgeType(x,y,stress_array, canvas_id)
{
        var c = document.getElementById(canvas_id);
        var ctx = c.getContext("2d");

        if(stress_array.isBorder == 0)
        {
           ctx.fillStyle = "#000000";
            
        }
        else
        {
           if(stress_array.type == "t")
           {
               //ctx.fillStyle = "white";
               ctx.fillStyle = colorLuminance("#FFFFFF", stress_array.shear);
           } 
           else if(stress_array.type == "c")
           {
               //ctx.fillStyle = "red";
               ctx.fillStyle = colorLuminance("#FF0000", stress_array.direct);
           }
           else
           {
               //ctx.fillStyle = "blue";
               ctx.fillStyle = colorLuminance("#0000FF", Math.abs(stress_array.direct));
           }
           //console.log("("+x+", "+y+") direct: " + stress_array.direct + " directvec: <"+stress_array.directvec.x+", "+stress_array.directvec.y+"> shear: " + stress_array.shear + " shearvec: <"+stress_array.shearvec.x+", "+stress_array.shearvec.y+"> type: " + stress_array.type);
            //ctx.fillRect(x, y, 1, 1);
        }    
            
        
        ctx.fillRect(x, y, 1, 1);
}

function drawPlateIDText(val_array, coords_array,canvas_id)
{
        var c = document.getElementById(canvas_id);
        var ctx = c.getContext("2d");
        ctx.font = "20px Arial";

        for (var i = 0; i < coords_array.length; i++ )
        {
           ctx.fillText(val_array[coords_array[i].x][coords_array[i].y],coords_array[i].x, coords_array[i].y); 

        }
            
        

}

function drawPlateAttributeText(attribute_array, coords_array,canvas_id)
{
        var c = document.getElementById(canvas_id);
        var ctx = c.getContext("2d");
        ctx.font = "15px Arial";
        

        for (var i = 0; i < coords_array.length; i++ )
        {

            var xt = (coords_array[i].x + translated) % width;
                        if (translated < 0)
                        {
                            xt = (width + (coords_array[i].x - (Math.abs(translated)%width))) % width;
                        }


           ctx.fillStyle = "#000000";
           //ctx.fillText(attribute_array[coords_array[i].x][coords_array[i].y].id,coords_array[i].x, coords_array[i].y - 20);
           if(attribute_array[coords_array[i].x][coords_array[i].y].isOceanic)
           {
                //ctx.fillText("O",coords_array[i].x - 20, coords_array[i].y + 5);
                ctx.fillText("O",xt - 20, coords_array[i].y + 5);     
           }
           else
           {
                //ctx.fillText("C",coords_array[i].x - 20, coords_array[i].y + 5);
                ctx.fillText("C",xt - 20, coords_array[i].y + 5);
           }
           
           //ctx.fillText(attribute_array[coords_array[i].x][coords_array[i].y].isOceanic,coords_array[i].x, coords_array[i].y);
           //ctx.fillText("(" + coords_array[i].x + ", " + coords_array[i].y + ")", coords_array[i].x + 10, coords_array[i].y);
           //ctx.fillText(attribute_array[coords_array[i].x][coords_array[i].y].baseEl.toFixed(3),coords_array[i].x, coords_array[i].y + 20);
           //ctx.fillText(attribute_array[coords_array[i].x][coords_array[i].y].vector.xcomp.toFixed(3) + ", " + attribute_array[coords_array[i].x][coords_array[i].y].vector.ycomp.toFixed(3), coords_array[i].x, coords_array[i].y + 40);
           ctx.beginPath();
           ctx.arc(xt,coords_array[i].y,5,0,2*Math.PI);
           ctx.moveTo(xt, coords_array[i].y);
           ctx.lineTo(xt + 20 * attribute_array[coords_array[i].x][coords_array[i].y].vector.xcomp.toFixed(3), coords_array[i].y + 20 * attribute_array[coords_array[i].x][coords_array[i].y].vector.ycomp.toFixed(3));
           //ctx.arc(coords_array[i].x,coords_array[i].y,5,0,2*Math.PI);
           ctx.strokeStyle = "#000000";
           ctx.stroke();
        }
            
        

}


function drawRockMap(x, xt, y, yt, type, canvas_id)
{

    var c = document.getElementById(canvas_id);
    var ctx = c.getContext("2d");

    switch(type)
    {
        case "sedimentary":
            ctx.fillStyle = "#FFF307";
            break;

        case "igneous":
            //ctx.fillStyle = "#0009C2";
            ctx.fillStyle = "#4da0ab";
            break;

        case "metamorphic":
            ctx.fillStyle = "#EF6876";
            break;

        default:
            ctx.fillStyle = "#000000";
            break;
    }
    if(ModifiedElevationArray[x][y] < sea_level)
    {
        ctx.fillStyle = "#1a2482";
    }

    ctx.fillRect(xt, yt, 1, 1);

}

function drawOreMap(x, xt, y, yt, type, canvas_id)
{
    //console.log("Oretype " + type);
    var c = document.getElementById(canvas_id);
    var ctx = c.getContext("2d");

    switch(type)
    {
        case "aluminum":
            ctx.fillStyle = "#34e5f5";
            break;
        case "tin":
            //ctx.fillStyle = "#0009C2";
            ctx.fillStyle = "#298970";
            break;
        case "copper":
            ctx.fillStyle = "#F7B946";
            break;
        case "silver":
            ctx.fillStyle = "#E7E7EE";
            break;
        case "lead":
            ctx.fillStyle = "#EAA19A";
            break;
        case "gold":
            ctx.fillStyle = "#F3F029";
            break;
        case "iron":
            ctx.fillStyle = "#ea4545";
            break;
        case "platinum":
            ctx.fillStyle = "#5bcd5e";
            break;
        case "coal":
            ctx.fillStyle = "#808080";
            break;
        case "diamond":
            ctx.fillStyle = "#cb5bea";
            break;
        default:
            //ctx.fillStyle = "#171815";
            ctx.fillStyle = "#000000";
            break;
    }
    if(ModifiedElevationArray[x][y] < sea_level)
    {
        ctx.fillStyle = "#1a2482";
    }

    ctx.fillRect(xt, yt, 1, 1);

}


function drawCities()
{
    //console.log("Here");
    //console.log(CityArray);
    document.getElementById("LangTreeTitle").innerHTML = "";
    document.getElementById("LangTree").innerHTML = "";

    var c = document.getElementById("myCanvas11");
    var ctx = c.getContext("2d");    

    for(var i = 0; i < CityArray.length; i++)
    {
        //ctx.fillStyle = "#000000";
        
        //CityArray[i].x -------> xt

        var xt = (CityArray[i].x + translated) % width;
                        if (translated < 0)
                        {
                            xt = (width + (CityArray[i].x - (Math.abs(translated)%width))) % width;
                        }


        var scaleDiffY = -1 * (height * (scale - 1) / scale);
        if (translatedY > 0)
        {
            translatedY = 0;
        }
        if (translatedY < scaleDiffY)
        {
            translatedY = Math.floor(scaleDiffY);
        }
        
        var yt = CityArray[i].y + translatedY;
        
        
        if(ModifiedElevationArray[CityArray[i].x][CityArray[i].y] < sea_level){
            continue;   
        }

        //console.log("i: " + i + " xt: " + xt + " yt: " + yt);
          
        ctx.beginPath();
        if(CityArray[i].population == "small"){
            ctx.arc(xt,yt,3,0,2*Math.PI);
               
        }
        else if(CityArray[i].population == "medium"){
            ctx.arc(xt,yt,5,0,2*Math.PI);    
        }
        else{
            ctx.arc(xt,yt,7,0,2*Math.PI);
            
        }
        //ctx.arc(xt,CityArray[i].y,5,0,2*Math.PI);
        ctx.fillStyle = LangFamilyColors[CityArray[i].family];//"#FF3BBF";
        ctx.fill();
        ctx.strokeStyle = "#000000"; //LangFamilyColors[CityArray[i].family];//"#000000";
        ctx.stroke();

        if(CityArray[i].isMutation){
            ctx.beginPath();
            if(CityArray[i].population == "small"){
            ctx.arc(xt,yt,1,0,2*Math.PI);
               
        }
        else if(CityArray[i].population == "medium"){
            ctx.arc(xt,yt,2,0,2*Math.PI);    
        }
        else{
            ctx.arc(xt,yt,3,0,2*Math.PI);
            
        }
            //ctx.arc(xt,yt,1,0,2*Math.PI);
            ctx.fillStyle = LangFamilyColors[CityArray[i].family];//"#FF3BBF";
            ctx.fill();
            ctx.strokeStyle = "#000000"; //LangFamilyColors[CityArray[i].family];//"#000000";
            ctx.stroke();    
        }

        /*if(CityArray[i].population == "small"){
            ctx.arc(xt,yt,4,0,2*Math.PI);
               
        }
        else if(CityArray[i].population == "medium"){
            ctx.arc(xt,yt,6,0,2*Math.PI);    
        }
        else{
            ctx.arc(xt,yt,8,0,2*Math.PI);
            
        }
        ctx.strokeStyle = "#000000"; //LangFamilyColors[CityArray[i].family];//"#000000";
        ctx.stroke();*/


        ctx.font = "15px Arial";
        ctx.strokeStyle = "black";
        ctx.lineWidth = 3;
        ctx.strokeText(CityArray[i].name, xt + 6, yt + 10);
        ctx.fillStyle = "white";
        ctx.fillText(CityArray[i].name, xt + 6, yt + 10);
        ctx.lineWidth = 1;

        /*if (i == (CityArray.length - 1))
        {
            document.getElementById("LangTree").innerHTML += CityArray[i].langTitle;
        }
        else
        {
            document.getElementById("LangTree").innerHTML += CityArray[i].langTitle + ", ";
        }*/

        
    }

    listLanguages();
    setWaitVisibility("hidden");

}

function listLanguages()
{
    document.getElementById("LangTreeTitle").innerHTML += "<h2>City Names by Language</h2><br><br>";
    for (var i = 0; i < LangArray.length; i++)
    {

        document.getElementById("LangTree").innerHTML += "<span style='color: "+LangFamilyColors[LangArray[i].family]+";'>"+LangArray[i].title+": "+LangArray[i].name+"</span>";
        for (var j = 0; j < LangArray[i].names.length; j++ )
        {
            document.getElementById("LangTree").innerHTML += "<br><span class='cityNames'>" + LangArray[i].names[j]+"</span>";    //<br> before span class
        }
        document.getElementById("LangTree").innerHTML += "<br><br>";

        if(textHeight(document.getElementById("LangTree").innerHTML, "15px Arial") > 300)
        {
            var heightratio = textHeight(document.getElementById("LangTree").innerHTML, "15px Arial") / 300;
            var numcolumns = Math.floor(heightratio) + 1;
            document.getElementById("LangTree").style["column-count"] = numcolumns;
            //alert("Num columns: " + numcolumns);
            //document.getElementById("LangTree").style["column-width"] = (width/numcolumns)+"px";
        }
    
    }

}

/*function drawWindArray(attribute_array, coords_array,canvas_id)
{
        var c = document.getElementById(canvas_id);
        var ctx = c.getContext("2d");
        ctx.font = "15px Arial";
        

        for (var i = 0; i < coords_array.length; i++ )
        {
           ctx.fillStyle = "#000000";
           ctx.fillText(attribute_array[coords_array[i].x][coords_array[i].y].isOceanic,coords_array[i].x, coords_array[i].y);
           ctx.fillText("(" + coords_array[i].x + ", " + coords_array[i].y + ")", coords_array[i].x + 10, coords_array[i].y);
           ctx.fillText(attribute_array[coords_array[i].x][coords_array[i].y].baseEl.toFixed(3),coords_array[i].x, coords_array[i].y + 20);
           ctx.fillText(attribute_array[coords_array[i].x][coords_array[i].y].vector.xcomp.toFixed(3) + ", " + attribute_array[coords_array[i].x][coords_array[i].y].vector.ycomp.toFixed(3), coords_array[i].x, coords_array[i].y + 40);
           ctx.beginPath();
           ctx.arc(coords_array[i].x - 10,coords_array[i].y,5,0,2*Math.PI);
           ctx.moveTo(coords_array[i].x - 10, coords_array[i].y);
           ctx.lineTo(coords_array[i].x-10 + 20 * attribute_array[coords_array[i].x][coords_array[i].y].vector.xcomp.toFixed(3), coords_array[i].y + 20 * attribute_array[coords_array[i].x][coords_array[i].y].vector.ycomp.toFixed(3));
           //ctx.arc(coords_array[i].x,coords_array[i].y,5,0,2*Math.PI);
           ctx.strokeStyle = "#000000";
           ctx.stroke();
        }
            
        

}

*/


function colorLuminance(hex, lum){

        lum = (1 - lum) * (-1); // get the amount to change luminosity by

        // Validate hex string
        hex = String(hex).replace(/[^0-9a-f]/gi, "");
        if (hex.length < 6){
            hex = hex.replace(/(.)/g, '$1$1');
        }
        lum = lum || 0;
    
        // Convert to decimal and change luminosity
        var rgb = "#", c;
        for (var i = 0; i < 3; ++i){
            c = parseInt(hex.substr(i * 2, 2), 16);
            c = Math.round(Math.min(Math.max(0, c + (c * lum)), 255)).toString(16);
            rgb += ("00" + c).substr(c.length);
        }

        return rgb;
}

function matrix(rows, cols, defaultValue){
        var arr = [];

        // Creates all lines:
        for (var i = 0; i < rows; i++)
        {
            // Creates an empty line
            arr.push([]);

            // Adds cols to the empty line:
            arr[i].push(new Array(cols));

            for (var j = 0; j < cols; j++){
                // Initializes:
                arr[i][j] = defaultValue;
            }
        }
        return arr;
}

function get_random_color(){
        function c()
        {
            var hex = Math.floor(Math.random() * 256).toString(16);
            return ("0" + String(hex)).substr(-2); // pad with zero
        }
        return "#" + c() + c() + c();
}

function drawBiomes_deprecated(x, y, biome, canvas_id, randColors){
        
        var c = document.getElementById(canvas_id);
        var ctx = c.getContext("2d");

        var OCEAN = "#1a2482";
        var BEACH = "#ffffcc";
        var SCORCHED = "#2e2d1f";
        var BARE = "#5a5a58";
        var TUNDRA = "#a0a090";
        var SNOW = "#eeeeee";
        var TEMPERATE_DESERT = "#ddcc99";
        var SHRUBLAND = "#6c9393";
        var TAIGA = "#999966";
        var GRASSLAND = "#a3c639";
        var TEMPERATE_DECIDUOUS_FOREST = "#238518";
        var TEMPERATE_RAIN_FOREST = "#0f9253";
        var SUBTROPICAL_DESERT = "#e5b784";
        var TROPICAL_SEASONAL_FOREST = "#008b04";
        var TROPICAL_RAIN_FOREST = "#235c41";



        if (document.getElementById('RandBiomColor').checked){

            var OCEAN = randColors[0];
            var BEACH = randColors[1];
            var SCORCHED = randColors[2];
            var BARE = randColors[3];
            var TUNDRA = randColors[4];
            var SNOW = randColors[5];
            var TEMPERATE_DESERT = randColors[6];
            var SHRUBLAND = randColors[7];
            var TAIGA = randColors[8];
            var GRASSLAND = randColors[9];
            var TEMPERATE_DECIDUOUS_FOREST = randColors[10];
            var TEMPERATE_RAIN_FOREST = randColors[11];
            var SUBTROPICAL_DESERT = randColors[12];
            var TROPICAL_SEASONAL_FOREST = randColors[13];
            var TROPICAL_RAIN_FOREST = randColors[14];
        }

        switch (biome){
            case "OCEAN":
                ctx.fillStyle = OCEAN;
                break;
            case "BEACH":
                ctx.fillStyle = BEACH;
                break;
            case "SCORCHED":
                ctx.fillStyle = SCORCHED;
                break;
            case "BARE":
                ctx.fillStyle = BARE;
                break;
            case "TUNDRA":
                ctx.fillStyle = TUNDRA;
                break;
            case "SNOW":
                ctx.fillStyle = SNOW;
                break;
            case "TEMPERATE_DESERT":
                ctx.fillStyle = TEMPERATE_DESERT;
                break;
            case "SHRUBLAND":
                ctx.fillStyle = SHRUBLAND;
                break;
            case "TAIGA":
                ctx.fillStyle = TAIGA;
                break;
            case "GRASSLAND":
                ctx.fillStyle = GRASSLAND;
                break;
            case "TEMPERATE_DECIDUOUS_FOREST":
                ctx.fillStyle = TEMPERATE_DECIDUOUS_FOREST;
                break;
            case "TEMPERATE_RAIN_FOREST":
                ctx.fillStyle = TEMPERATE_RAIN_FOREST;
                break;
            case "SUBTROPICAL_DESERT":
                ctx.fillStyle = SUBTROPICAL_DESERT;
                break;
            case "TROPICAL_SEASONAL_FOREST":
                ctx.fillStyle = TROPICAL_SEASONAL_FOREST;
                break;
            case "TROPICAL_RAIN_FOREST":
                ctx.fillStyle = TROPICAL_RAIN_FOREST;
                break;
            default:
                ctx.fillStyle = "#000000";
                break;
        }

        ctx.fillRect(x, y, 1, 1);

}

function drawVor(vor_result, plate_coords, canvas_id, width, height, noisearr)
{
    //window.alert("got here");
        var c = document.getElementById(canvas_id);
        var ctx = c.getContext("2d");
        ctx.clearRect(0, 0, width, height);

        
        
            //edges
            ctx.beginPath();
            ctx.strokeStyle = "#000000";
            var edges = vor_result.edges;
            var iEdge = edges.length;
            var edge;
            var v;
            var ve;
            var slope;
            var intercept;
            var disp_amnt_x;
            var disp_amnt_y;
            var middlex;
            var middley;

            while (iEdge--)
            {
                //window.alert("here");
                edge = edges[iEdge];
                v = edge.va;
                ve = edge.vb;


                //console.log("Vx: " + v.x + " Vy: " + v.y + " Vex: " + ve.x + " Vey: " + ve.y);
                if (!((v.y == 0) && (ve.y == 0)) && !((v.y == height) && (ve.y == height))  && !((v.x == 0) && (ve.x == 0)) && !((v.x == width) && (ve.x == width)))
                {
                    

                    slope = (ve.y - v.y) / (ve.x - v.x);
                    intercept = ve.y - (((ve.y - v.y) * ve.x) / (ve.x - v.x));
                    var seg_length = Math.sqrt((ve.x - v.x) * (ve.x - v.x) + (ve.y - v.y) * (ve.y - v.y));

                    //if ((ve.y - v.y) != 0 || (ve.x - v.x) != 0)
                    //{
                        ctx.moveTo(v.x, v.y);

                        /*if(slope == Infinity || slope == -Infinity)
                        {
                        middlex = 0;
                        }
                        else
                        {
                        middlex = .1*(ve.x - v.x);
                        middley = .1*(ve.y - v.y);
                        }
                        if(slope != Infinity || slope != -Infinity)
                        {
                        var segment_x = v.x;
                        var segment_y = v.y;
                        //window.alert(noisearr[segment_x][segment_y]);
                        for(var o = 0; o < 10; o++)
                        {
                        
                        //disp_amnt = Math.floor(Math.random()*2)-1;
                        //middley = slope * middlex + intercept;
                        //disp_amnt_x = (middlex / Math.sqrt(middlex * middlex + middley * middley) + Math.floor(Math.random()*10)-5);
                        //disp_amnt_y = (middley / Math.sqrt(middlex * middlex + middley * middley) + Math.floor(Math.random()*10)-5);

                        var unit_vec_x = middlex / Math.sqrt(middlex * middlex + middley * middley);
                        var unit_vec_y = middley / Math.sqrt(middlex * middlex + middley * middley);

                        var selector = Math.floor(Math.random() * 2);

                        if (selector >= 1)
                        {
                        disp_amnt_x = (unit_vec_x * (seg_length / 10) + noisearr[10][10] * unit_vec_y);
                        disp_amnt_y = (unit_vec_y * (seg_length / 10) + noisearr[10][10] * unit_vec_x * -1);
                        }
                        else
                        {
                        disp_amnt_x = (unit_vec_x * (seg_length / 10) + noisearr[10][10] * unit_vec_y * -1);
                        disp_amnt_y = (unit_vec_y * (seg_length / 10) + noisearr[10][10] * unit_vec_x);
                        }

                        segment_x += disp_amnt_x;
                        segment_y += disp_amnt_y;
                        window.alert("x: "+segment_x+ " y: "+ segment_y);
                        ctx.lineTo(segment_x, segment_y);
                        }
                        }*/

                        //window.alert("s: " + slope + " int: " + intercept);
                        ctx.lineTo(ve.x, ve.y);
                        }
                    //}
            }
            ctx.stroke();

        
        // vertices
        /*ctx.beginPath();
        ctx.fillStyle = "red";
        var vertices = vor_result.vertices,
            iVertex = vertices.length;
        while (iVertex--) {
            v = vertices[iVertex];
            ctx.rect(v.x-1,v.y-1,3,3);
            }
        ctx.fill();
        // sites / plate coords
        ctx.beginPath();
        ctx.fillStyle = "#44f";
        var sites = vor_result.sites,
            iSite = sites.length;
        while (iSite--) {
            v = sites[iSite];
            ctx.rect(v.x-2/3,v.y-2/3,2,2);
            }
        ctx.fill();*/
        
        
    return ctx.getImageData(0,0,width,height);
}

function drawPlates(vor_result, plate_coords, canvas_id, width, height, doDraw, noisearr)
{
        //window.alert(noisearr);
    
        var c = document.getElementById(canvas_id);
        var ctx = c.getContext("2d");
        ctx.clearRect(0, 0, width, height);

        var pd = new Perlin(Math.random());
        


        if (doDraw == true)
        {
            //edges
            ctx.beginPath();
            ctx.strokeStyle = "#000000";
            var edges = vor_result.edges;
            var iEdge = edges.length;
            var edge;
            var v;
            var ve;
            var slope;
            var intercept;
            var disp_amnt_x;
            var disp_amnt_y;
            var middlex;
            var middley;

            while (iEdge--)
            {
                //window.alert("here");
                edge = edges[iEdge];
                v = edge.va;
                v.x = Math.floor(v.x);
                v.y = Math.floor(v.y);
                ctx.moveTo(v.x, v.y);
                ve = edge.vb;
                ve.x = Math.floor(ve.x);
                ve.y = Math.floor(ve.y);
                
                



                var xvec = ve.x - v.x;
                var yvec = ve.y - v.y;

                if (xvec == 0)
                {

                    slope = "inf";
                    var perpslope = 0;
                    var seg_length = yvec;
                }
                else if (yvec == 0)
                {
                    slope = 0;
                    var perpslope = "inf";
                    var seg_length = xvec;
                }
                else
                {
                    slope = yvec / xvec;
                    //intercept = ve.y - (((ve.y - v.y) * ve.x) / (ve.x - v.x));
                    var perpslope = (-1) * (1 / slope);
                    var seg_length = Math.floor(Math.sqrt((ve.x - v.x) * (ve.x - v.x) + (ve.y - v.y) * (ve.y - v.y)));
                    //window.alert("Vax: " + v.x + " Vay: " + v.y + " Vbx: " + ve.x + " Vby: " + ve.y);
                    //window.alert(seg_length);
                }
                var xstep = xvec / seg_length;
                var ystep = yvec / seg_length;

                var newposx = v.x + xstep;var newposy = v.y + ystep;

                //window.alert("xstep: " + xstep + " ystep: " + ystep);

                for (var i = 0; i < seg_length; i++ )
                {
                    //window.alert("here: " + i);
                   //var arrtest = noisearr[newposx];
                   //window.alert(arrtest);
                   // window.alert("newposx: " + newposx + " newposy: " + newposy);
                   if(newposy == height)
                   {
                       newposy = height - 1;
                   }
                   if(newposx == width)
                   {
                       newposx = width - 1;
                   }
                   
                   //var turbvec = (noisearr[newposx][newposy]*2)-1;
                   //var turbvec = 10;
                   //var turbvec = Math.random() * 50 - 25;
                   
                   var dx = 8 * newposx / width; var dy = 8 * newposy / height;
                   //var dvalue = pd.noise(dx, dy, 0) + .5 * pd.noise(2 * dx, 2 * dy, 0) + .25 * pd.noise(4 * dx, 4 * dy, 0) + .125 * pd.noise(8 * dx, 8 * dy, 0) + .0625 * pd.noise(16 * dx, 16 * dy, 0);
                   var dvalue = pd.noise(dx, dy, 0);
                   dvalue /= (1 + .5 + .25);
                   dvalue = (dvalue * 100)-50;
                   //var range2 = 1.9;                                         //create turbulence noise
                   //dvalue = (1 - (Math.abs((dvalue - .4) * range2 - 1)))*50-25;

                   var turbvec = dvalue;

                   if (xvec == 0)
                   {
                       var ypart = 0;
                       var xpart = turbvec;
                       
                       //window.alert("Slope INF - xpart: "+ xpart + " ypart: "+ypart+" turbvec: "+turbvec);
                       //window.alert("newposx: " + newposx + " newposy: " + newposy);
                   }
                   else if (yvec == 0)
                   {
                       var ypart = turbvec;
                       var xpart = 0;
                       
                       //window.alert("Slope 0 - xpart: "+ xpart + " ypart: "+ypart+" turbvec: "+turbvec);
                       //window.alert("newposx: " + newposx + " newposy: " + newposy);
                   }
                   else
                   {
                       var ypart = Math.sqrt((turbvec * turbvec * perpslope * perpslope) / (perpslope * perpslope + 1));
                       var xpart = ypart / perpslope;
                       
                       //window.alert("Slope NRML - xpart: "+ xpart + " ypart: "+ypart+" turbvec: "+turbvec);
                       //window.alert("newposx: " + newposx + " newposy: " + newposy);
                   }
                   
                   ctx.lineTo(newposx + xpart, newposy + ypart);
                   //newposx += xstep;newposy += ystep;
                   newposx += xstep;newposy += ystep;
                   
                     
                   //window.alert("xpart: "+ xpart + " ypart: "+ypart+" turbvec: "+turbvec);         
                   



                }
                
                
                ctx.lineTo(ve.x, ve.y);
            }
            ctx.stroke();

        }
        // vertices
        /*ctx.beginPath();
        ctx.fillStyle = "red";
        var vertices = vor_result.vertices,
            iVertex = vertices.length;
        while (iVertex--) {
            v = vertices[iVertex];
            ctx.rect(v.x-1,v.y-1,3,3);
            }
        ctx.fill();
        // sites / plate coords
        ctx.beginPath();
        ctx.fillStyle = "#44f";
        var sites = vor_result.sites,
            iSite = sites.length;
        while (iSite--) {
            v = sites[iSite];
            ctx.rect(v.x-2/3,v.y-2/3,2,2);
            }
        ctx.fill();*/
        
        
    
}

function drawPlates2(vor_result, plate_coords, canvas_id, width, height, doDraw, noisearr)
{
        //window.alert(noisearr);
    
        var c = document.getElementById(canvas_id);
        var ctx = c.getContext("2d");
        ctx.clearRect(0, 0, width, height);

        var pd = new Perlin(Math.random());
        


        if (doDraw == true)
        {
            //edges
            ctx.beginPath();
            ctx.strokeStyle = "#000000";
            var edges = vor_result.edges;
            var iEdge = edges.length;
            var edge;
            var v;
            var ve;
            var slope;
            var intercept;
            var disp_amnt_x;
            var disp_amnt_y;
            var middlex;
            var middley;

            while (iEdge--)
            {
                //window.alert("here");
                edge = edges[iEdge];
                v = edge.va;
                v.x = Math.floor(v.x);
                v.y = Math.floor(v.y);
                ctx.moveTo(v.x, v.y);
                ve = edge.vb;
                ve.x = Math.floor(ve.x);
                ve.y = Math.floor(ve.y);
                
                



                var xvec = ve.x - v.x;
                var yvec = ve.y - v.y;

                if (xvec == 0)
                {

                    slope = "inf";
                    var perpslope = 0;
                    var seg_length = yvec;
                }
                else if (yvec == 0)
                {
                    slope = 0;
                    var perpslope = "inf";
                    var seg_length = xvec;
                }
                else
                {
                    slope = yvec / xvec;
                    //intercept = ve.y - (((ve.y - v.y) * ve.x) / (ve.x - v.x));
                    var perpslope = (-1) * (1 / slope);
                    var seg_length = Math.floor(Math.sqrt((ve.x - v.x) * (ve.x - v.x) + (ve.y - v.y) * (ve.y - v.y)));
                    //window.alert("Vax: " + v.x + " Vay: " + v.y + " Vbx: " + ve.x + " Vby: " + ve.y);
                    //window.alert(seg_length);
                }
                var xstep = xvec / seg_length;
                var ystep = yvec / seg_length;

                //var xstep = 1;
                //var ystep = 1;

                var newposx = v.x + xstep;var newposy = v.y + ystep;

                //window.alert("xstep: " + xstep + " ystep: " + ystep);

                for (var i = 0; i < seg_length; i++ )
                {
                    //window.alert("here: " + i);
                   //var arrtest = noisearr[newposx];
                   //window.alert(arrtest);
                   // window.alert("newposx: " + newposx + " newposy: " + newposy);
                   if(newposy == height)
                   {
                       newposy = height - 1;
                   }
                   if(newposx == width)
                   {
                       newposx = width - 1;
                   }
                   
                   //var turbvec = (noisearr[newposx][newposy]*2)-1;
                   //var turbvec = 10;
                   //var turbvec = Math.random() * 50 - 25;
                   
                   var dx = 8 * (newposx) / width; var dy = 8 * (newposy) / height;
                   //var dvalue = pd.noise(dx, dy, 0) + .5 * pd.noise(2 * dx, 2 * dy, 0) + .25 * pd.noise(4 * dx, 4 * dy, 0) + .125 * pd.noise(8 * dx, 8 * dy, 0) + .0625 * pd.noise(16 * dx, 16 * dy, 0);
                   var dvalue = pd.noise(dx, dy, 0) + .5 * pd.noise(2 * dx, 2 * dy, 0);// + .25 * pd.noise(4 * dx, 4 * dy, 0);
                   //dvalue /= (1 + .5 + .25 +.125 +.0625);
                   //dvalue = (dvalue * 80)-40;
                   //window.alert(dvalue);
                   var range2 = 1.9;                                         //create turbulence noise
                   dvalue = (Math.abs((dvalue - .4) * range2 - 1)*50 - 25);

                   var turbvec = dvalue;

                   if (xvec == 0)
                   {
                       var ypart = 0;
                       var xpart = turbvec;
                       
                       //window.alert("Slope INF - xpart: "+ xpart + " ypart: "+ypart+" turbvec: "+turbvec);
                       //window.alert("newposx: " + newposx + " newposy: " + newposy);
                   }
                   else if (yvec == 0)
                   {
                       var ypart = turbvec;
                       var xpart = 0;
                       
                       //window.alert("Slope 0 - xpart: "+ xpart + " ypart: "+ypart+" turbvec: "+turbvec);
                       //window.alert("newposx: " + newposx + " newposy: " + newposy);
                   }
                   else
                   {
                       var ypart = Math.sqrt((turbvec * turbvec * perpslope * perpslope) / (perpslope * perpslope + 1));
                       var xpart = ypart / perpslope;
                       
                       //window.alert("Slope NRML - xpart: "+ xpart + " ypart: "+ypart+" turbvec: "+turbvec);
                       //window.alert("newposx: " + newposx + " newposy: " + newposy);
                   }
                   
                   ctx.lineTo(newposx + xpart, newposy + ypart);
                   //newposx += xstep;newposy += ystep;
                   newposx += xstep;newposy += ystep;
                   
                     
                   //window.alert("xpart: "+ xpart + " ypart: "+ypart+" turbvec: "+turbvec);         
                   



                }
                
                
                ctx.lineTo(ve.x, ve.y);
            }
            ctx.stroke();

        }
        // vertices
        /*ctx.beginPath();
        ctx.fillStyle = "red";
        var vertices = vor_result.vertices,
            iVertex = vertices.length;
        while (iVertex--) {
            v = vertices[iVertex];
            ctx.rect(v.x-1,v.y-1,3,3);
            }
        ctx.fill();
        // sites / plate coords
        ctx.beginPath();
        ctx.fillStyle = "#44f";
        var sites = vor_result.sites,
            iSite = sites.length;
        while (iSite--) {
            v = sites[iSite];
            ctx.rect(v.x-2/3,v.y-2/3,2,2);
            }
        ctx.fill();*/
        
        
    
}




function addVorTurbulence(BaseArray, width, height, scalefactor)
{

    console.log(BaseArray);
    var VorTurbArray = matrix(width, height, 0);


    //var randtest1 = Math.random();
    //var randtest2 = Math.random();
    
    //var pvtx = new Perlin(PerlinSeeds[0]);
    //var pvty = new Perlin(PerlinSeeds[1]);

    var pvtx = new Perlin(Math.random());
    var pvty = new Perlin(Math.random());

    //console.log("r1, r2: " + randtest1 + " " + randtest2);

    for(var y = 0; y < height; y++)
    {
        for(var x = 0; x < width; x++)
        {

            var wrap_flag_x = false;
            var wrap_flag_y = false;

            var px = x;var py = y;

            var vtx = 8 * x / width; var vty = 8 * y / height;
            var vtxvalue = pvtx.noise(vtx, vty, 0) + .5 * pvtx.noise(2 * vtx, 2 * vty, 0) + .25 * pvtx.noise(4 * vtx, 4 * vty, 0) + .125 * pvtx.noise(8 * vtx, 8 * vty, 0) + .0625 * pvtx.noise(16 * vtx, 16 * vty, 0);
            var vtyvalue = pvty.noise(vtx, vty, 0) + .5 * pvty.noise(2 * vtx, 2 * vty, 0) + .25 * pvty.noise(4 * vtx, 4 * vty, 0) + .125 * pvty.noise(8 * vtx, 8 * vty, 0) + .0625 * pvty.noise(16 * vtx, 16 * vty, 0);

            

            px += scalefactor * vtxvalue;
            py += scalefactor * vtyvalue;

            var pxint = Math.floor(px);
            var pyint = Math.floor(py);


            //pxint = pxint % width;
            //pyint = pyint % height;

            if(pxint >= width)                                      //||-------------------TO CREATE PLATES THAT WRAP IN X DIRECTION
            {                                                                   // comment out wrap_flag_x = true in the pxint >= width if statement
                pxint -= width;                                                 // divide scalefactor by a number (2,4, etc) when assigning scaleoffsetx and when checking for wrap_flag_x in x+scalefactor >= width if statement
                //wrap_flag_x = true;                                           //can also remove scalefactor
                
            }
            if(pyint >= height)
            {
                pyint -= height;
                wrap_flag_y = true;
                
            }

            
            

            var scaleoffsety = (y + scalefactor) % height;
            var scaleoffsetx = (x + scalefactor /*+ scalefactor*20*/) % width;

            if((y + scalefactor) >= height)
            {
                wrap_flag_y = true; 
            }
            if((x + scalefactor) >= width)
            {
                //wrap_flag_x = true;
            }

            if (wrap_flag_x == true && wrap_flag_y == true)
            {
                VorTurbArray[scaleoffsetx][scaleoffsety] = BaseArray[scaleoffsetx][scaleoffsety];
            }
            else if(wrap_flag_x == true)
            {
                VorTurbArray[scaleoffsetx][scaleoffsety] = BaseArray[scaleoffsetx][pyint];
                
            }
            else if(wrap_flag_y == true)
            {
                VorTurbArray[scaleoffsetx][scaleoffsety] = BaseArray[pxint][scaleoffsety];
                
            }

            else{
                VorTurbArray[scaleoffsetx][scaleoffsety] = BaseArray[pxint][pyint];
            }

            
            
            
            //for(var i = 0; i < plate_coords.length; i++)        //check that plate coords don't get moved to other plates
           // {
            //    if(scaleoffsetx == plate_coords[i].x && scaleoffsety == plate_coords[i].y)
            //    {
            //        VorTurbArray[scaleoffsetx][scaleoffsety] = BaseArray[scaleoffsetx][scaleoffsety];
            //        break;
            //    }

            //}


            /*px += 50*vtxvalue;
            py += 50*vtyvalue;

            var pxint = Math.floor(px);
            var pyint = Math.floor(py);
            
            if(pxint >= width)
            {
                pxint -= width;
                //pxint = width - 1;
            }
            if(pyint >= height)
            {
                pyint -= height;
                //pyint = height - 1;
            }
            */
            //VorTurbArray[x][y] = BaseArray[pxint][pyint];

            //console.log("value: " + VorTurbArray[x][y]);
            //console.log("value: " + VorTurbArray[x][y] + " x/y: ("+x+", "+y+") pxint/pyint: ("+pxint+", "+pyint+")");
            

        }
    }

    return VorTurbArray;


}

function bitConvert(array, width, height)
{
    //window.alert("here");
    var morphArray = [];
    var position = 0;

    for(var i = 0; i < height; i++)
    {
        for (var j = 0; j < width; j++ )
        {
            position = (width * i) + j;
            morphArray[position] = array[j][i];


            if(morphArray[position] <= 0)
            {
                morphArray[position] = 0;
            }
            else
            {
                morphArray[position] = 1;
            }
            //console.log("v: "+morphArray[position]);

        }
        

    }



    return morphArray;

}


function setPlateIDs(vor_result, width, height)
{
   
    var PlateIDArray = matrix(width, height, 0);
    var cells = vor_result.cells;
    var iCell = cells.length;
    var isIn;

    for (var y = 0; y < height; y++)        //iterate across image
   {
        for (var x = 0; x < width; x++)
        {
            
            for (var i = 0; i < iCell; i++)  // go through each continent
           {

                isIn = cells[i].pointIntersection(x, y);
                if(isIn == 1 || isIn == 0)
                {
                    PlateIDArray[x][y] = i;
                    break;

                }
               // else if(isIn == 0)
               // {
               //     PlateIDArray[x][y] = (iCell + 1);
               // }
           }
        }
    }

    return PlateIDArray;
}



function setWrapPlateIDs(vor_result, width, height)
{
   
    var PlateIDArray = matrix(width, height, 0);
    var cells = vor_result.cells;
    var iCell = cells.length;
    //window.alert("w: " + width + " iCell: " + iCell);
    var isIn;

    for (var y = 0; y < height; y++)        //iterate across image
   {
        for (var x = 0; x < width; x++)
        {
            
            for (var i = 0; i < iCell; i++)  // go through each continent
           {

                isIn = cells[i].pointIntersection(x, y);
                if((isIn == 1) || isIn == 0)
                {
                    //if (i >= (iCell / 2))
                    if(i % 2 != 0)
                    {
                    //    var adjusted_i = i - (iCell / 2) - 1;
                    //    if(adjusted_i < 0)
                    //    {
                    //        adjusted_i = (iCell / 2) + adjusted_i;
                    //    }
                    //    PlateIDArray[x][y] = adjusted_i;//i - (iCell/2);
                        PlateIDArray[x][y] = (i - 1)/2;

                    }
                    else
                    {
                        PlateIDArray[x][y] = i/2;
                        
                    }
                    break;
                }
                //else if(isIn == 0)
                //{
                //    PlateIDArray[x][y] = 0;
                //}
               // else if(isIn == 0)
               // {
               //     PlateIDArray[x][y] = (iCell + 1);
               // }
           }
        }
    }

    return PlateIDArray;
}

function cropWrapPlateIDs(WrapArray, width, height)
{
    var cropPlateIDArray = matrix(width, height, 0);

    for(var y = 0; y < height; y++)
    {
        for(var x = 0; x < width; x++)
        {

            cropPlateIDArray[x][y] = WrapArray[x + (width / 2)][y];


        }
    }

    //fix y = 0 row
    for (x = 0; x < width; x++ )
    {
        cropPlateIDArray[x][0] = cropPlateIDArray[x][1];

    }

        return cropPlateIDArray;
}


function getEdges(img_array, width, height)
{

    var edge_array = matrix(width, height, 0);

    var ul, u, ur, l, r, bl, b, br = 0;
    var wrapxl = 0;
    var wrapxr = 0;
    var wrapyu = 0;
    var wrapyb = 0;

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
            if(y + 1 > height)
            {
                wrapyb = y;
                //wrapyb = y % height - 1;
            }

            if(img_array[wrapxl][wrapyu] != img_array[x][y] || img_array[x][wrapyu] != img_array[x][y] || img_array[wrapxr][wrapyu] != img_array[x][y]
            || img_array[wrapxl][y] != img_array[x][y] || img_array[wrapxr][y] != img_array[x][y] || img_array[wrapxl][wrapyb] != img_array[x][y] 
            || img_array[x][wrapyb] != img_array[x][y] || img_array[wrapxr][wrapyb] != img_array[x][y])
            {

                edge_array[x][y] = 1;

            }
            else
            {
                edge_array[x][y] = 0;
            }
            //console.log(x+", "+y+": "+img_array[x][y]+" ul: " + img_array[wrapxl][wrapyu]);
        }

    }

    return edge_array;
    

}

function setPlateAttributes(plate_array, edge_array, width, height, percent_ocean, plate_count)
{
    var attribute_array = matrix(width, height, 0);
    var plate_vectors = [];
    var oceaniclist = [];
    var baseElevation = [];

    for (var i = 0; i < plate_count; i++ )
    {
        plate_vectors[i] = { xcomp: (2 * Math.random() - 1), ycomp: (2 * Math.random() - 1) };

        if(Math.random() < percent_ocean)
        {
            
            oceaniclist[i] = 1;
            baseElevation[i] = .55 * Math.random();

        }
        else
        {
            
            oceaniclist[i] = 0;
            baseElevation[i] = .55 * Math.random() + .45;

        }


    }

    for (var y = 0; y < height; y++)
    {
        for (var x = 0; x < width; x++)
        {

            attribute_array[x][y] = { id: plate_array[x][y], vector: { xcomp: plate_vectors[plate_array[x][y]].xcomp, ycomp: plate_vectors[plate_array[x][y]].ycomp }, isOceanic: oceaniclist[plate_array[x][y]], baseEl: baseElevation[plate_array[x][y]] };

        }
    }


        return attribute_array;
}



function setPlateBoundaryStress(attribute_array, edge_array, plate_coords_array, width, height)
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
        console.log("result[i]: " + result[i]);
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
    //console.log(doubled_id + " " + missing_id);


    //console.log("doubled ids/missing ids/missingplatecoords/platecoordsarray");
    //console.log(doubled_id + " " + missing_id);
    //console.log(doubled_ids);
    //console.log(missing_ids);
    //console.log(missingplatecoords);
    //console.log(plate_coords_array);

    outerLoop:
    for (var p = 0; p < plate_coords_array.length; p++ )
    {

           //console.log("PCA X, PCA Y #"+p);
           //console.log(plate_coords_array[p].x + ", " + plate_coords_array[p].y);
           //console.log("Att Array @ PCAX, PCAY");
           //console.log(attribute_array[plate_coords_array[p].x][plate_coords_array[p].y]);

           //if(repeat_check.includes(attribute_array[plate_coords_array[p].x][plate_coords_array[p].y].id))

        for (var d = 0; d < doubled_ids.length; d++)
        {
            if (repeat_check[p] == doubled_ids[d])
            {
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
                //console.log("doublecount > 1");
                ordered_plates[missing_ids[d]] = { x: plate_coords_array[missingplatecoords[d]].x, y: plate_coords_array[missingplatecoords[d]].y };
                doublecount[doubled_ids[d]] = 1;
                continue outerLoop;
            }
        }
           ordered_plates[attribute_array[plate_coords_array[p].x][plate_coords_array[p].y].id] = {x: plate_coords_array[p].x, y: plate_coords_array[p].y};  //sometimes throwing error, "cannot read property y of undefined"
           //repeat_check.push(attribute_array[plate_coords_array[p].x][plate_coords_array[p].y].id);
    }

   

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
            
        }
    }
    console.log("Par: <" + lowpar + ", " + highpar + "> Perp: <" + lowperp + ", " + highperp + ">");

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

/*function FindPlateNeighbors(stress_array, width, height)
{   

    //var plate_neighbors = [];
    //var plate_neighbors = matrix(width, height, 0);  

    for(var y  = 0; y < height; y++)
    {
        for(var x = 0; x < width; x++)
        {
            
          
          
          if(stress_array[x][y].isBorder == 1)
          {
              
          }
          
          
          
          
          
          //if(plate_neighbors.indexOf(stress_array[x][y].pair_id.id0) >= 0)
          //{



              //continue;

          //}
          //else
          //{   



              
              //plate_neighbors[stress_array[x][y].pair_id.id0] = {id: stress_array[x][y].pair_id.id0, numNeigbors: ,neighbors: }

          //}    



        }
    }



}
*/


function createPerlinElevation(pn, width, height)
{

    var ElevationArray = matrix(width, height, 0);
    
    for (var y = 0; y < height; y++)
    {
        for (var x = 0; x < width; x++)
        {   

            //var nx = 6*(x)/width; 
            var ny = 4*y / height;
            //nx = Math.cos(2 * Math.PI * (x / width));
            //nx = nx % width;
            nx = Math.cos((x * 2 * Math.PI) / width); 
            nz = Math.sin((x * 2 * Math.PI) / width);

            //Create Elevation Noise
            //var value = pn.noise(nx, ny, 0) + .5 * pn.noise(2 * nx, 2 * ny, 0) + .25 * pn.noise(4 * nx, 4 * ny, 0) + .125 * pn.noise(8 * nx, 8 * ny, 0) + .0625 * pn.noise(16 * nx, 16 * ny, 0);
            var value = pn.noise(nx, ny, nz) + .5 * pn.noise(2 * nx, 2 * ny, 2*nz) + .25 * pn.noise(4 * nx, 4 * ny, 4*nz) + .125 * pn.noise(8 * nx, 8 * ny, 8*nz) + .0625 * pn.noise(16 * nx, 16 * ny, 16*nz);

            //value /= (1 + .5 + .25 + .125 + .0625);
            value /= 1.28;
            value = Math.pow(value, 2);

            ElevationArray[x][y] = value;
        }
    }

    return ElevationArray;
}


function modifyElevation(elevation_array, stress_array, attribute_array, width, height, doEdges, baseMod)
{

    var modifiedElevation = matrix(width, height, 0);
    var edgeonlyarray = [];
    var distavg = 0;

    //console.log(stress_array);

    /*for(var y = 0; y < height; y++)
    {
        
        for(var x = 0; x < width; x++)
        {

            modifiedElevation[x][y] = elevation_array[x][y]*attribute_array[x][y].baseEl;   //modify noise field based on height of generated plates

            var near_edge_distance = 0;
            //var lowdistance = Infinity;
            //var lowedgetype = "";
            var lowedgearray = {distance: Infinity, point: {x: 0, y: 0}};

            //-----------------Find distance to nearest edge, location of nearest pixel in that edge
            for(var q = 0; q < height; q++)
            {
                for(var p = 0; p < width; p++)
                {
                    
                    //console.log("(" + p + ", " + q + ") "+ stress_array[p][q].isBorder);
                    
                    if(stress_array[p][q].isBorder == 1 && attribute_array[x][y].id == stress_array[p][q].pair_id.id0)
                    {
                        near_edge_distance = Math.sqrt( ((p-x)*(p-x)) + ((q-y)*(q-y)) );
                        if(near_edge_distance < lowedgearray.distance)
                        {
                            //lowdistance = near_edge_distance;
                            //lowedgetype = stress_array[p][q].type;\
                            lowedgearray = { distance: near_edge_distance, point: { x: p, y: q} };
                        }

                    }



                }
            }

            // determine height modifiers based on boundary type
            if(stress_array[lowedgearray.point.x][lowedgearray.point.y].type == "c")
            {
                if(attribute_array[stress_array[lowedgearray.point.x][lowedgearray.point.y].neighbor.x][stress_array[lowedgearray.point.x][lowedgearray.point.y].neighbor.y].isOceanic == 1 
                && attribute_array[x][y].isOceanic == 1)
                {
                    




                }
                else if(attribute_array[stress_array[lowedgearray.point.x][lowedgearray.point.y].neighbor.x][stress_array[lowedgearray.point.x][lowedgearray.point.y].neighbor.y].isOceanic == 0 
                && attribute_array[x][y].isOceanic == 0)
                {
                    


                }
                else
                {
                    


                }


            }
            else if(stress_array[lowedgearray.point.x][lowedgearray.point.y].type == "d")
            {
                

            }
            else
            {
                


            }
            


        }
    }*/

    for (var i = 0; i < width; i++ )            //creating an array of edges only. Convert later to find nearest edge by increasing radius instead
    {
        
        for(var j = 0; j < height; j++)
        {
            
            if(stress_array[i][j].isBorder == 1)
            {
                edgeonlyarray.push({ x: i, y: j, id: stress_array[i][j].pair_id.id0, neighbor_id: stress_array[i][j].pair_id.id1, type: stress_array[i][j].type, isOceanic: attribute_array[i][j].isOceanic });
                //console.log("Type: " + stress_array[i][j].type + " direct: " + stress_array[i][j].direct + " shear: " + stress_array[i][j].shear);

            }



        }
    }


        var pn = new Perlin(Math.random());    

        for (var y = 0; y < height; y++)
        {

            for (var x = 0; x < width; x++)
            {



                var distance = 0;       //distance to nearest edge not bordering ocean
                var fulldistance = 0;   //distance to nearest edge
                var distcompare = Infinity;
                var fulldistcompare = Infinity;
                var edgepoint = [];
                var fulledgepoint = [];

                for (var a = 0; a < edgeonlyarray.length; a++ )
                {
                    
                    if(attribute_array[x][y].id == edgeonlyarray[a].id) //&& attribute_array[stress_array[edgeonlyarray[a].x][edgeonlyarray[a].y].neighbor.x][stress_array[edgeonlyarray[a].x][edgeonlyarray[a].y].neighbor.y].baseEl * elevation_array[stress_array[edgeonlyarray[a].x][edgeonlyarray[a].y].neighbor.x][stress_array[edgeonlyarray[a].x][edgeonlyarray[a].y].neighbor.y] >= .35)
                    //&& attribute_array[stress_array[edgeonlyarray[a].x][edgeonlyarray[a].y].neighbor.x][stress_array[edgeonlyarray[a].x][edgeonlyarray[a].y].neighbor.y].isOceanic != 1 && edgeonlyarray[a].isOceanic != 1)
                    {
                        distance = Math.sqrt(((edgeonlyarray[a].x - x) * (edgeonlyarray[a].x - x)) + ((edgeonlyarray[a].y - y) * (edgeonlyarray[a].y - y)));
                        if(distance < distcompare)
                        {
                            distcompare = distance;
                            edgepoint[0] = { x: edgeonlyarray[a].x, y: edgeonlyarray[a].y };

                        }

                    }
                    //else if(attribute_array[x][y].id == edgeonlyarray[a].id)
                    //{
                        
                        //fulldistance = Math.sqrt(((edgeonlyarray[a].x - x) * (edgeonlyarray[a].x - x)) + ((edgeonlyarray[a].y - y) * (edgeonlyarray[a].y - y)));
                        //if(fulldistance < fulldistcompare)
                        //{
                        //    fulldistcompare = fulldistance;
                        //    fulledgepoint = { x: edgeonlyarray[a].x, y: edgeonlyarray[a].y };

                        //}

                   // }


                }

                    distavg += distcompare;

                    //var modifiedbaseel = attribute_array[x][y].baseEl + ((1 / (.2 * distcompare + 1)) * (1 - attribute_array[x][y].baseEl));
                    //var modifiedbaseel = attribute_array[x][y].baseEl + (1 - (.05 * distcompare))*(1 - attribute_array[x][y].baseEl);
                    //var modifiedbaseel = (1 / (.001 * (distcompare*distcompare) + 1));
                    var modifiedbaseel = attribute_array[x][y].baseEl + ((1 / (.01 * (distcompare*distcompare) + 1)) * (1 - attribute_array[x][y].baseEl));
                    //console.log("Distcompare: " + distcompare);

                    if (baseMod == true)
                    {
                        

                        if (elevation_array[x][y] * attribute_array[x][y].baseEl >= sea_level)
                        {
                            modifiedElevation[x][y] = elevation_array[x][y] * modifiedbaseel;    //modify noise field based on height of generated plates
                            modifiedElevation[x][y] = modifiedElevation[x][y] + (.15*(1-modifiedElevation[x][y])) - .12;
                        }
                       // else
                       // {
                            //modifiedElevation[x][y] = elevation_array[x][y] * attribute_array[x][y].baseEl;
                            //modifiedElevation[x][y] = modifiedElevation[x][y] + (.15*(1-modifiedElevation[x][y]));

                       // }

                       else if (elevation_array[x][y] * attribute_array[x][y].baseEl < .2)
                       {

                           modifiedElevation[x][y] = elevation_array[x][y] * attribute_array[x][y].baseEl;

                           var shoreline_flag = false;
                           var radius = 10;
                           

                           /*for (var g = -radius; g <= radius; g++ )
                           {
                               
                               for(var h = -radius; h <=radius; h++)
                               {
                                    var wrapg = x + g;
                                    var wraph = y + h;


                            if (wrapg < 0)
                            {
                                //wrapxl = width - x;
                                wrapg = width + wrapg;
                            }
                            if (wrapg >= width)
                            {
                                wrapg = wrapg % width;
                                //wrapxr = x % width - 1;
                            }
                            if (wraph < 0)                   //y doesn't wrap, so don't sample values on other side of image
                            {
                                wraph = 0;
                                //wrapyu = height - y;
                            }
                            if (wraph >= height)
                            {
                                wraph = height - 1;
                                //wrapyb = y % height - 1;
                            }
                                   if(elevation_array[wrapg][wraph] * attribute_array[wrapg][wraph].baseEl >= .35)
                                   {

                                       //console.log(elevation_array[wrapg][wraph]);
                                       //shoreline_flag = true;
                                       break;
                                   }


                               }
                           }
                           */
                           //if(shoreline_flag == true)
                           //{
                               
                                //modifiedElevation[x][y] = elevation_array[x][y] * modifiedbaseel;
                                //if(modifiedElevation[x][y] >= .35)
                               // {
                               //     modifiedElevation[x][y] = .34;


                               // }
                        //   }
                           


                           //if(modifiedElevation[x][y]*attribute_array[x][y].baseEl <= .35)
                           //{
                               
                               
                               //----------------------------------------------------------------------------
                               /*if (elevation_array[x][y] * modifiedbaseel < .35)
                               {
                                   modifiedElevation[x][y] = elevation_array[x][y] * modifiedbaseel;
                               }
                               else
                               {
                                   modifiedElevation[x][y] = elevation_array[x][y] * attribute_array[x][y].baseEl;
                               }*/



                               //----------------------------------------------------------------------------------
                           //modifiedElevation[x][y] = elevation_array[x][y] * modifiedbaseel;
                           //else
                           //{
                           //    modifiedElevation[x][y] = .34;
                               //modifiedElevation[x][y] = modifiedElevation[x][y] * (attribute_array[x][y].baseEl);
                           //}
                           //}
                           
                       }
                       else
                       {
                           
                             modifiedElevation[x][y] = elevation_array[x][y] * attribute_array[x][y].baseEl;

                       }

                    }
                    else
                    {
                        modifiedElevation[x][y] = elevation_array[x][y] * attribute_array[x][y].baseEl;
                    }

                    if (doEdges == true)
                    {

                        //var pn = new Perlin(Math.random());

                        var ny = 4*y / height;
                        //nx = Math.cos(2 * Math.PI * (x / width));
                        //nx = nx % width;
                        nx = Math.cos((x * 2 * Math.PI) / width); 
                        nz = Math.sin((x * 2 * Math.PI) / width);

                        //Create Elevation Noise
                        //var value = pn.noise(nx, ny, 0) + .5 * pn.noise(2 * nx, 2 * ny, 0) + .25 * pn.noise(4 * nx, 4 * ny, 0) + .125 * pn.noise(8 * nx, 8 * ny, 0) + .0625 * pn.noise(16 * nx, 16 * ny, 0);
                        var value = pn.noise(nx, ny, nz) + .5 * pn.noise(2 * nx, 2 * ny, 2*nz) + .25 * pn.noise(4 * nx, 4 * ny, 4*nz) + .125 * pn.noise(8 * nx, 8 * ny, 8*nz) + .0625 * pn.noise(16 * nx, 16 * ny, 16*nz);

                        //value /= (1 + .5 + .25 + .125 + .0625);
                        value /= 1.28;
                        value = Math.pow(value, 2);
                        value = value * .4 + .6;
                        //console.log(value);


                        //console.log("ME1 doEdges = TRUE");
                        //console.log(edgepoint);
                        // determine height modifiers based on boundary type
                        if (stress_array[edgepoint[0].x][edgepoint[0].y].type == "c")
                        {
                            if (attribute_array[stress_array[edgepoint[0].x][edgepoint[0].y].neighbor.x][stress_array[edgepoint[0].x][edgepoint[0].y].neighbor.y].isOceanic == 1
                        && attribute_array[x][y].isOceanic == 1)
                            {

                                //modifiedElevation[x][y] += .5 * ((1.2 * stress_array[edgepoint.x][edgepoint.y].direct * stress_array[edgepoint.x][edgepoint.y].direct) / (distcompare + 3 * stress_array[edgepoint.x][edgepoint.y].direct));
                                //modifiedElevation[x][y] += .5*((stress_array[edgepoint[0].x][edgepoint[0].y].direct * stress_array[edgepoint[0].x][edgepoint[0].y].direct * (distcompare + 7)) / ((.2*stress_array[edgepoint[0].x][edgepoint[0].y].direct) * ((distcompare + 13)*(distcompare + 13))));
                                modifiedElevation[x][y] += value*.8*stress_array[edgepoint[0].x][edgepoint[0].y].direct * (.2 / (.003 * (distcompare * distcompare) + 1));

                            }
                            else if (attribute_array[stress_array[edgepoint[0].x][edgepoint[0].y].neighbor.x][stress_array[edgepoint[0].x][edgepoint[0].y].neighbor.y].isOceanic == 0
                        && attribute_array[x][y].isOceanic == 0)
                            {


                                //modifiedElevation[x][y] += ((1.2 * stress_array[edgepoint.x][edgepoint.y].direct * stress_array[edgepoint.x][edgepoint.y].direct) / (distcompare + 3 * stress_array[edgepoint.x][edgepoint.y].direct));
                                //modifiedElevation[x][y] += ((stress_array[edgepoint[0].x][edgepoint[0].y].direct * stress_array[edgepoint[0].x][edgepoint[0].y].direct * (distcompare + 7)) / ((.2 * stress_array[edgepoint[0].x][edgepoint[0].y].direct) * ((distcompare + 13)*(distcompare + 13))));
                                modifiedElevation[x][y] += value*stress_array[edgepoint[0].x][edgepoint[0].y].direct * (.2 / (.003 * (distcompare * distcompare) + 1));
                            }
                            else
                            {

                                //modifiedElevation[x][y] += .35 * ((1.2 * stress_array[edgepoint.x][edgepoint.y].direct * stress_array[edgepoint.x][edgepoint.y].direct) / (distcompare + 3 * stress_array[edgepoint.x][edgepoint.y].direct));
                                //modifiedElevation[x][y] += .35 * ((stress_array[edgepoint[0].x][edgepoint[0].y].direct * stress_array[edgepoint[0].x][edgepoint[0].y].direct * (distcompare + 7)) / ((.2 * stress_array[edgepoint[0].x][edgepoint[0].y].direct) * ((distcompare + 13)*(distcompare + 13))));
                                modifiedElevation[x][y] += value*.6*stress_array[edgepoint[0].x][edgepoint[0].y].direct * (.2 / (.003 * (distcompare * distcompare) + 1));
                            }


                        }
                        else if (stress_array[edgepoint[0].x][edgepoint[0].y].type == "d")
                        {


                        }
                        else
                        {



                        }
                        //modifiedElevation[x][y] *= modifiedbaseel;

                    }

                /*if (doEdges == true)
                {
                    var near_edge_distance = 0;
                    //var lowdistance = Infinity;
                    //var lowedgetype = "";
                    var lowedgearray = { distance: Infinity, point: { x: 0, y: 0} };

                    //-----------------Find distance to nearest edge, location of nearest pixel in that edge
                    for (var q = -25; q < 25; q++)
                    {
                        for (var p = -25; p < 25; p++)
                        {


                            wrapp = x + p;
                            wrapq = y + q;


                            if (wrapp < 0)
                            {
                                //wrapxl = width - x;
                                wrapp = 0;
                            }
                            if (wrapp >= width)
                            {
                                wrapp = width - 1;
                                //wrapxr = x % width - 1;
                            }
                            if (wrapq < 0)                   //y doesn't wrap, so don't sample values on other side of image
                            {
                                wrapq = 0;
                                //wrapyu = height - y;
                            }
                            if (wrapq >= height)
                            {
                                wrapq = height - 1;
                                //wrapyb = y % height - 1;
                            }

                            //console.log("(" + p + ", " + q + ") "+ stress_array[p][q].isBorder);

                            if (stress_array[wrapp][wrapq].isBorder == 1 && attribute_array[x][y].id == stress_array[wrapp][wrapq].pair_id.id0)
                            {
                                near_edge_distance = Math.sqrt(((wrapp - x) * (wrapp - x)) + ((wrapq - y) * (wrapq - y)));
                                if (near_edge_distance < lowedgearray.distance)
                                {
                                    //lowdistance = near_edge_distance;
                                    //lowedgetype = stress_array[p][q].type;\
                                    lowedgearray = { distance: near_edge_distance, point: { x: wrapp, y: wrapq} };
                                }

                            }



                        }
                    }
                    
                    // determine height modifiers based on boundary type
                    if (stress_array[lowedgearray.point.x][lowedgearray.point.y].type == "c")
                    {
                        if (attribute_array[stress_array[lowedgearray.point.x][lowedgearray.point.y].neighbor.x][stress_array[lowedgearray.point.x][lowedgearray.point.y].neighbor.y].isOceanic == 1
                && attribute_array[x][y].isOceanic == 1)
                        {

                            modifiedElevation[x][y] += .5 * ((1.2 * stress_array[lowedgearray.point.x][lowedgearray.point.y].direct * stress_array[lowedgearray.point.x][lowedgearray.point.y].direct) / (lowedgearray.distance + 3 * stress_array[lowedgearray.point.x][lowedgearray.point.y].direct));



                        }
                        else if (attribute_array[stress_array[lowedgearray.point.x][lowedgearray.point.y].neighbor.x][stress_array[lowedgearray.point.x][lowedgearray.point.y].neighbor.y].isOceanic == 0
                && attribute_array[x][y].isOceanic == 0)
                        {


                            modifiedElevation[x][y] += ((1.2 * stress_array[lowedgearray.point.x][lowedgearray.point.y].direct * stress_array[lowedgearray.point.x][lowedgearray.point.y].direct) / (lowedgearray.distance + 3 * stress_array[lowedgearray.point.x][lowedgearray.point.y].direct));

                        }
                        else
                        {

                            modifiedElevation[x][y] += .35 * ((1.2 * stress_array[lowedgearray.point.x][lowedgearray.point.y].direct * stress_array[lowedgearray.point.x][lowedgearray.point.y].direct) / (lowedgearray.distance + 3 * stress_array[lowedgearray.point.x][lowedgearray.point.y].direct));

                        }


                    }
                    else if (stress_array[lowedgearray.point.x][lowedgearray.point.y].type == "d")
                    {


                    }
                    else
                    {



                    }


                }*/
            }
        }
        //console.log("distavg: " + (distavg / (width * height)));
    return modifiedElevation;

}

function modifyElevation2(elevation_array, stress_array, attribute_array, width, height, doEdges, baseMod)
{
    var edgeElevation = matrix(width, height, 0);
    var modifiedElevation = matrix(width, height, 0);
    var edgeonlyarray = [];
    var distavg = 0;
    
    edgeElevation = elevation_array;
    var ccount = 0;
    var dcount = 0;
    var tcount = 0;

    var distance = 0;       //distance to nearest edge not bordering ocean
    var fulldistance = 0;   //distance to nearest edge
    var distcompare = Infinity;
    var fulldistcompare = Infinity;
    var edgepoint = [];
    var fulledgepoint = [];

    var pn = new Perlin(Math.random());


    //var t0 = performance.now();
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
    console.log("EOA length " + edgeonlyarray.length);
    //var t1 = performance.now();
    //console.log("--Create Edge Only Array Loop: " + (t1 - t0));
    
    //var loopcount = 0;    


    //t0 = performance.now();
    for (var y = 0; y < height; y++)
    {

        for (var x = 0; x < width; x++)
        {

            distance = 0;       //distance to nearest edge not bordering ocean
            fulldistance = 0;   //distance to nearest edge
            distcompare = Infinity;
            fulldistcompare = Infinity;
            edgepoint = [];
            fulledgepoint = [];
                //console.log("doEdges: " + doEdges);

            
            //t2 = performance.now();
            for (var a = 0; a < edgeonlyarray.length; a++ )
            {
                    
                    //if(attribute_array[stress_array[edgeonlyarray[a].x][edgeonlyarray[a].y].neighbor.x][stress_array[edgeonlyarray[a].x][edgeonlyarray[a].y].neighbor.y].baseEl * elevation_array[stress_array[edgeonlyarray[a].x][edgeonlyarray[a].y].neighbor.x][stress_array[edgeonlyarray[a].x][edgeonlyarray[a].y].neighbor.y] >= .35)
                if(attribute_array[x][y].id == edgeonlyarray[a].id && attribute_array[stress_array[edgeonlyarray[a].x][edgeonlyarray[a].y].neighbor.x][stress_array[edgeonlyarray[a].x][edgeonlyarray[a].y].neighbor.y].baseEl * elevation_array[stress_array[edgeonlyarray[a].x][edgeonlyarray[a].y].neighbor.x][stress_array[edgeonlyarray[a].x][edgeonlyarray[a].y].neighbor.y] >= sea_level)
                    //&& attribute_array[stress_array[edgeonlyarray[a].x][edgeonlyarray[a].y].neighbor.x][stress_array[edgeonlyarray[a].x][edgeonlyarray[a].y].neighbor.y].isOceanic != 1 && edgeonlyarray[a].isOceanic != 1)
                {
                    distance = Math.sqrt(((edgeonlyarray[a].x - x) * (edgeonlyarray[a].x - x)) + ((edgeonlyarray[a].y - y) * (edgeonlyarray[a].y - y)));
                    if(distance < distcompare)
                    {
                        distcompare = distance;
                        edgepoint[0] = { x: edgeonlyarray[a].x, y: edgeonlyarray[a].y };

                    }

                }
                    //else if(attribute_array[x][y].id == edgeonlyarray[a].id)
                    //{
                        
                        //fulldistance = Math.sqrt(((edgeonlyarray[a].x - x) * (edgeonlyarray[a].x - x)) + ((edgeonlyarray[a].y - y) * (edgeonlyarray[a].y - y)));
                        //if(fulldistance < fulldistcompare)
                        //{
                        //    fulldistcompare = fulldistance;
                        //    fulledgepoint = { x: edgeonlyarray[a].x, y: edgeonlyarray[a].y };

                        //}

                   // }

                //loopcount++;
            }
            //t3 = performance.now();
            //console.log("-- --Find Distance Loop: " + (t3 - t2));

            distavg += distcompare;

            if (doEdges == true)
            {

                var ny = 4*y / height;
                        //nx = Math.cos(2 * Math.PI * (x / width));
                        //nx = nx % width;
                nx = Math.cos((x * 2 * Math.PI) / width); 
                nz = Math.sin((x * 2 * Math.PI) / width);

                        //Create Elevation Noise
                        //var value = pn.noise(nx, ny, 0) + .5 * pn.noise(2 * nx, 2 * ny, 0) + .25 * pn.noise(4 * nx, 4 * ny, 0) + .125 * pn.noise(8 * nx, 8 * ny, 0) + .0625 * pn.noise(16 * nx, 16 * ny, 0);
                        //var value = pn.noise(nx, ny, nz) + .5 * pn.noise(2 * nx, 2 * ny, 2*nz) + .25 * pn.noise(4 * nx, 4 * ny, 4*nz) + .125 * pn.noise(8 * nx, 8 * ny, 8*nz) + .0625 * pn.noise(16 * nx, 16 * ny, 16*nz);
                var value = .125 * pn.noise(8 * nx, 8 * ny, 8*nz) + .0625 * pn.noise(16 * nx, 16 * ny, 16*nz) + .03125 * pn.noise(32 * nx, 32 * ny, 32 * nz);

                        //value /= (1 + .5 + .25 + .125 + .0625);
                        //value /= 1.28;
                        //value = Math.pow(value, 2);
                        //value = value * .4 + .6;
                        //value -= .4;
                value *= 2.5;
                        //var contrast = (1.2 * (.2 + 1)) / (1 * (1.2 - .2));
                value = (1.2 * (value - .5 + 1)) / (1 * (1.2 - (value - .5)));

                        //console.log("doEdges == true");
                        //console.log(edgepoint);


                 edgeElevation[x][y] *= attribute_array[x][y].baseEl;
                        
                        
                        
                        
                        // determine height modifiers based on boundary type
                        if (stress_array[edgepoint[0].x][edgepoint[0].y].type == "c")// && distcompare < 20)
                        {
                            ccount++;
                            var added_elev = stress_array[edgepoint[0].x][edgepoint[0].y].direct * (.3 / (.001 * (distcompare * distcompare) + 1)) - .0087;

                            if (attribute_array[stress_array[edgepoint[0].x][edgepoint[0].y].neighbor.x][stress_array[edgepoint[0].x][edgepoint[0].y].neighbor.y].isOceanic == 1
                        && attribute_array[x][y].isOceanic == 1)
                            {
                                //console.log("PreEl 11 " + edgeElevation[x][y] + " Direct: "+ stress_array[edgepoint[0].x][edgepoint[0].y].direct + " Dist: "+ distcompare);
                                //modifiedElevation[x][y] += .5 * ((1.2 * stress_array[edgepoint.x][edgepoint.y].direct * stress_array[edgepoint.x][edgepoint.y].direct) / (distcompare + 3 * stress_array[edgepoint.x][edgepoint.y].direct));
                                //edgeElevation[x][y] += .5*((stress_array[edgepoint[0].x][edgepoint[0].y].direct * stress_array[edgepoint[0].x][edgepoint[0].y].direct * (distcompare + 7)) / ((.2*stress_array[edgepoint[0].x][edgepoint[0].y].direct) * ((distcompare + 13)*(distcompare + 13))));
                                //edgeElevation[x][y] += .5*stress_array[edgepoint[0].x][edgepoint[0].y].direct * (.2 / (.003 * (distcompare * distcompare) + 1));
                                //console.log("PostEl 11 " + edgeElevation[x][y]);

                                //---------maybe try multiplicative value ie (1.5 * edgeElevation at 0 distance, falling off until hits a limit, say ~30 px
                                if(added_elev > 0)
                                {

                                    edgeElevation[x][y] += .3 *value * added_elev;
                                    console.log("value: " + value + " added_elev: " + added_elev + " distance: " + distcompare);

                                }


                            }
                            else if (attribute_array[stress_array[edgepoint[0].x][edgepoint[0].y].neighbor.x][stress_array[edgepoint[0].x][edgepoint[0].y].neighbor.y].isOceanic == 0
                        && attribute_array[x][y].isOceanic == 0)
                            {

                                //console.log("PreEl 00 " + edgeElevation[x][y] + " Direct: "+ stress_array[edgepoint[0].x][edgepoint[0].y].direct + " Dist: "+ distcompare);
                                //modifiedElevation[x][y] += ((1.2 * stress_array[edgepoint.x][edgepoint.y].direct * stress_array[edgepoint.x][edgepoint.y].direct) / (distcompare + 3 * stress_array[edgepoint.x][edgepoint.y].direct));
                                //edgeElevation[x][y] += ((stress_array[edgepoint[0].x][edgepoint[0].y].direct * stress_array[edgepoint[0].x][edgepoint[0].y].direct * (distcompare + 7)) / ((.2 * stress_array[edgepoint[0].x][edgepoint[0].y].direct) * ((distcompare + 13)*(distcompare + 13))));
                                //edgeElevation[x][y] += stress_array[edgepoint[0].x][edgepoint[0].y].direct * (.2 / (.003 * (distcompare * distcompare) + 1));
                                //console.log("PostEl 00 " + edgeElevation[x][y]);

                             if(added_elev > 0)
                                {

                                    edgeElevation[x][y] += value * added_elev;
                                    console.log("value: " + value + " added_elev: " + added_elev  + " distance: " + distcompare);

                                }

                            }
                            else
                            {
                                //console.log("PreEl 01 " + edgeElevation[x][y] + " Direct: "+ stress_array[edgepoint[0].x][edgepoint[0].y].direct + " Dist: "+ distcompare);
                                //modifiedElevation[x][y] += .35 * ((1.2 * stress_array[edgepoint.x][edgepoint.y].direct * stress_array[edgepoint.x][edgepoint.y].direct) / (distcompare + 3 * stress_array[edgepoint.x][edgepoint.y].direct));
                                //edgeElevation[x][y] += .35 * ((stress_array[edgepoint[0].x][edgepoint[0].y].direct * stress_array[edgepoint[0].x][edgepoint[0].y].direct * (distcompare + 7)) / ((.2 * stress_array[edgepoint[0].x][edgepoint[0].y].direct) * ((distcompare + 13)*(distcompare + 13))));
                                //edgeElevation[x][y] += .35 * stress_array[edgepoint[0].x][edgepoint[0].y].direct * (.2 / (.003 * (distcompare * distcompare) + 1));
                                //console.log("PostEl 01 " + edgeElevation[x][y]);

                                if(added_elev > 0)
                                {

                                    edgeElevation[x][y] += sea_level * value * added_elev;
                                    console.log("value: " + value + " added_elev: " + added_elev  + " distance: " + distcompare);

                                }
                            }


                        }
                        else if (stress_array[edgepoint[0].x][edgepoint[0].y].type == "d")
                        {
                            dcount++;

                        }
                        else
                        {
                            tcount++;


                        }

                        //return edgeElevation;
                }                
                    
                    
                    //var modifiedbaseel = attribute_array[x][y].baseEl + ((1 / (.2 * distcompare + 1)) * (1 - attribute_array[x][y].baseEl));
                    //var modifiedbaseel = attribute_array[x][y].baseEl + (1 - (.05 * distcompare))*(1 - attribute_array[x][y].baseEl);
                    //var modifiedbaseel = (1 / (.001 * (distcompare*distcompare) + 1));
                    var modifiedbaseel = attribute_array[x][y].baseEl + ((1 / (.01 * (distcompare*distcompare) + 1)) * (1 - attribute_array[x][y].baseEl));
                    //console.log("Distcompare: " + distcompare);

                    if (baseMod == true)
                    {

                        //console.log("eE xy " + edgeElevation[x][y] + " e_a xy " + elevation_array[x][y]);
                        if (edgeElevation[x][y] * attribute_array[x][y].baseEl >= sea_level)
                        {
                            modifiedElevation[x][y] = edgeElevation[x][y] * modifiedbaseel;     
                            //modify noise field based on height of generated plates
                            modifiedElevation[x][y] = modifiedElevation[x][y] + (.15*(1-modifiedElevation[x][y])) - .12;
                        }
                      
                                     
                               
                        /*else if (edgeElevation[x][y] * attribute_array[x][y].baseEl < .2)
                        {

                           modifiedElevation[x][y] = edgeElevation[x][y] * attribute_array[x][y].baseEl;

                           var shoreline_flag = false;
                           var radius = 10;
                           
                        
                                                      
                        }*/
                        else
                        {
                           
                             modifiedElevation[x][y] = edgeElevation[x][y] * attribute_array[x][y].baseEl;

                        }
                        
                    }
                    else
                    {
                        modifiedElevation[x][y] = elevation_array[x][y] * attribute_array[x][y].baseEl;
                    }

                    

                
            }
        }

        //t1 = performance.now();
        //console.log("--Modify El Loop: " + (t1 - t0));
        //console.log("C: " + ccount + " D: " + dcount + " T: " + tcount);
        console.log("distavg: " + (distavg / (width * height)));
        //console.log("Distance Loop Count: " + loopcount);
        return modifiedElevation;
        //return edgeElevation;

}


function modifyElevation3(elevation_array, stress_array, attribute_array, neighbor_array, width, height)
{

    //console.log(elevation_array);
    //console.log(attribute_array);

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


    console.log("G_I_V");
    console.log(gradient_init_value);
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




              function stop()
              {
                  clearInterval(timer);
              }
                //progresspercent += 1/(height*width);
                //drawLoadBar(2 / 9 + (progresspercent/12));


                

               
        }
    }
    console.log("LOW R VALUE " + lowrvalue);
    console.log("High R VALUE " + highrvalue);
    console.log("LOW O VALUE " + lowovalue);
    console.log("High O VALUE " + highovalue);
    console.log("LOW O2 VALUE " + lowovalue2);
    console.log("High O2 VALUE " + highovalue2);
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
    return modifiedElevation;
    //return modifiedPressure;    

}


function findPlateNeighbors(stress_array, width, height)
{
    var neighbor_array = [];

    for (var y = 0; y < height; y++ )
    {
        for(var x = 0; x < width; x++)
        {


            if (stress_array[x][y].isBorder == 1)       //only border pixels contain neighbor info
            {
                var index = -1;

                for (var i = 0, len = neighbor_array.length; i < len; i++)
                {                 //for each x,y value of stress array, see if neighbor pair has already been added to neighbor_array
                    if (neighbor_array[i].id == stress_array[x][y].pair_id.id0 && neighbor_array[i].neighbor == stress_array[x][y].pair_id.id1)
                    {
                        index = i;
                        break;
                    }
                }

                if (index < 0)  //if the current neighbor pair is not added to neighbor_array, add it
                {
                    neighbor_array.push({ id: stress_array[x][y].pair_id.id0, neighbor: stress_array[x][y].pair_id.id1, direct_force: stress_array[x][y].direct, shear_force: stress_array[x][y].shear, type: stress_array[x][y].type});
                }
            }


        }
    }

    neighbor_array.sort(function (a, b) { return a.id - b.id || a.neighbor - b.neighbor;});

    var neighbor_check_array = [];
    neighbor_check_array = neighbor_array;

    /*for (var j = 0; j < neighbor_array.length; j++ )
    {
        for(var k = 0; k < neighbor_array.length; k++)
        {
            
            if(neighbor_array[j].id == neighbor_check_array[k].neighbor && neighbor_array[j].neighbor == neighbor_check_array[k].id)
            {

                console.log("NA[" + j + "] DF: " + neighbor_array[j].direct_force + " NCA[" + k + "] DF: " + neighbor_check_array[k].direct_force);
                //console.log("got here");
                if(neighbor_array[j].direct_force != neighbor_check_array[k].direct_force)
                {
                    console.log("NA[" + j + "] DF: " + neighbor_array[j].direct_force + " NCA[" + k + "] DF: " + neighbor_check_array[k].direct_force);

                }

            }



        }

    }*/

        return neighbor_array;
}



function averageElevation(elevation_array, width, height)
{
    var averagedElevation = matrix(width, height, 0);

    for (var y = 0; y < height; y++)
    {

        for (var x = 0; x < width; x++)
        {


            /*var wrapxl = x - 1;
            var wrapxr = x + 1;
            var wrapyu = y - 1;
            var wrapyb = y + 1;

            
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

            var avg = 0;

            var conditions = [elevation_array[wrapxl][wrapyu], elevation_array[x][wrapyu], elevation_array[wrapxr][wrapyu], elevation_array[wrapxl][y], elevation_array[wrapxr][y], elevation_array[wrapxl][wrapyb], elevation_array[x][wrapyb], elevation_array[wrapxr][wrapyb]];

            for (var i = 0; i < conditions.length; i++)
            {

            avg += conditions[i];

            }
            avg += elevation_array[x][y];
            avg /= (conditions.length + 1);
            averagedElevation[x][y] = avg;*/

            if (elevation_array[x][y] > sea_level)
            {

                var avg = 0;
                var radius = 3;
                var count = 1;
                var valarray = [];
                var median = 0;

                for (var q = (-1 * radius); q <= radius; q++)
                {
                    for (var p = (-1 * radius); p <= radius; p++)
                    {


                        var wrapp = x + p;
                        var wrapq = y + q;


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
                            wrapq = 0;
                            //wrapyu = height - y;
                        }
                        if (wrapq >= height)
                        {
                            wrapq = height - 1;
                            //wrapyb = y % height - 1;
                        }

                        if (elevation_array[wrapp][wrapq] > sea_level)
                        {

                            //valarray.push(elevation_array[wrapp][wrapq]);
                            //if(p == 0 && q == 0)
                            //{
                                avg += elevation_array[wrapp][wrapq];  
                           //}
                            //else
                            //{
                            //    avg += 3*elevation_array[wrapp][wrapq];
                            //}
                            
                            count++;
                        }
                        //else
                        //{
                        //    avg 
                        //}



                    }
                }

                //valarray.sort(function (a, b) { return a - b; });
                //median = valarray[Math.floor(valarray.length / 2)];
                //console.log(median);

                //avg /= (2*radius + 1)*(2*radius + 1);
                avg /= count;
                //avg /= 3 * ((2 * radius + 1) * (2 * radius + 1) - 1) + 1;
                //averagedElevation[x][y] = avg + (.7*avg);
                averagedElevation[x][y] = avg;
                //averagedElevation[x][y] = median;

            }
            else
            {
                averagedElevation[x][y] = elevation_array[x][y];
            }
        }
    }

    return averagedElevation;

}

function averageArray(in_array, elevation_array, width, height, avg_radius, elevation_flag)
{
    var averagedArray = matrix(width, height, 0);

    for (var y = 0; y < height; y++)
    {

        for (var x = 0; x < width; x++)
        {


            /*var wrapxl = x - 1;
            var wrapxr = x + 1;
            var wrapyu = y - 1;
            var wrapyb = y + 1;

            
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

            var avg = 0;

            var conditions = [elevation_array[wrapxl][wrapyu], elevation_array[x][wrapyu], elevation_array[wrapxr][wrapyu], elevation_array[wrapxl][y], elevation_array[wrapxr][y], elevation_array[wrapxl][wrapyb], elevation_array[x][wrapyb], elevation_array[wrapxr][wrapyb]];

            for (var i = 0; i < conditions.length; i++)
            {

            avg += conditions[i];

            }
            avg += elevation_array[x][y];
            avg /= (conditions.length + 1);
            averagedElevation[x][y] = avg;*/

            //if (elevation_array[x][y] > .35)
            //{

                var avg = 0;
                var radius = avg_radius;
                var count = 1;
                var valarray = [];
                var median = 0;

                for (var q = (-1 * radius); q <= radius; q++)
                {
                    for (var p = (-1 * radius); p <= radius; p++)
                    {


                        var wrapp = x + p;
                        var wrapq = y + q;


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
                            wrapq = 0;
                            //wrapyu = height - y;
                        }
                        if (wrapq >= height)
                        {
                            wrapq = height - 1;
                            //wrapyb = y % height - 1;
                        }


                        if(elevation_flag == true){
                            if (elevation_array[wrapp][wrapq] > sea_level)   //UNCOMMENT
                            {                                          //UNCOMMENT

                                //valarray.push(elevation_array[wrapp][wrapq]);
                                //if(p == 0 && q == 0)
                                //{
                                avg += in_array[wrapp][wrapq];
                                //}
                                //else
                                //{
                                //    avg += 3*elevation_array[wrapp][wrapq];
                                //}

                                count++;

                           }                                           //UNCOMMENT
                       }
                       else
                       {
                           avg += in_array[wrapp][wrapq];
                           count++;
                       } 
                        
                        }
                        //else
                        //{
                        //    avg 
                        //}



                    }
               // }

               // else{
                           
               //     avg = in_array[x][y];
               // }

                //valarray.sort(function (a, b) { return a - b; });
                //median = valarray[Math.floor(valarray.length / 2)];
                //console.log(median);

                //avg /= (2*radius + 1)*(2*radius + 1);
                avg /= count;
                //avg /= 3 * ((2 * radius + 1) * (2 * radius + 1) - 1) + 1;
                //averagedElevation[x][y] = avg + (.7*avg);
                averagedArray[x][y] = avg;
                //averagedElevation[x][y] = median;

            //}
            //else
            //{
            //    averagedElevation[x][y] = elevation_array[x][y];
            //}
        }
    }

    return averagedArray;

}


function generateBaseTemperature(pn, elevation_array, width, height, canvas_id, doElev)
{
    //var baseTempColortoNum = [];
    var baseTemps = matrix(width, height);

    var c = document.getElementById(canvas_id);
    var ctx = c.getContext("2d");

    

    ctx.canvas.width += 1;
    ctx.canvas.width -= 1;
    //ctx.fillStyle = "#FFFFFF";
    //ctx.fillRect(0, 0, width, height);

    if (isheatrandom == "true")
    {
        heat_factor = Math.pow(Math.random(), (1 / 3));
        console.log("random heat factor");
    }
    else
    {
        heat_factor = Math.pow(heat_factor, (1 / 3));
        console.log("user set heat factor");
    }
    console.log("Heat Factor: " + heat_factor);

    //ctx.clearRect(0, 0, width, height);

    var grd = ctx.createLinearGradient(0, 0, 0, height/2);
    grd.addColorStop(0, "black");
    grd.addColorStop(heat_factor, "white");
    grd.addColorStop(1, "white");

    var grd1 = ctx.createLinearGradient(0, height/2, 0, height);
    grd1.addColorStop(0, "white");
    grd1.addColorStop((1 - heat_factor), "white");
    grd1.addColorStop(1, "black");

    
    //ctx.fillStyle = "#FFFFFF";
    ctx.fillStyle = grd;
    ctx.fillRect(0, 0, width, height/2);
    //ctx.fillStyle = "#FFFFFF";
    ctx.fillStyle = grd1;
    ctx.fillRect(0, height/2, width, height/2);

    

    var imgData=ctx.getImageData(0,0,width,height);

    //console.log("grd, grd1 ");
    //console.log(grd);
    //console.log(grd1);

    /*for (var i = 0; i < imgData.data.length; i+=4 )
    {

        baseTempColortoNum = imgData.data[i] / 255;


    }
    console.log(baseTempColortoNum);
    console.log(width * height + " = " + imgData.data.length);*/

    //var pn = new Perlin(Math.random());
    //console.log(imgData);


    for (var y = 0; y < height; y++ )
    {
        
        for(var x = 0; x < width; x++)
        {


            var ny = 4*y / height;
            
            nx = Math.cos((x * 2 * Math.PI) / width); 
            nz = Math.sin((x * 2 * Math.PI) / width);

            
            var value = pn.noise(nx, ny, nz) + .5 * pn.noise(2 * nx, 2 * ny, 2*nz) + .25 * pn.noise(4 * nx, 4 * ny, 4*nz) + .125 * pn.noise(8 * nx, 8 * ny, 8*nz) + .0625 * pn.noise(16 * nx, 16 * ny, 16*nz);

            value /= 1.28;
            value = Math.pow(value, 2);



            baseTemps[x][y] = 1.15*value * (imgData.data[4 * (width * y + x)] / 255);
            //baseTemps[x][y] = (imgData.data[4 * (width * y + x)] / 255);

            if (doElev == true)
            {
                if (elevation_array[x][y] > .9)
                {
                    baseTemps[x][y] -= .4 * elevation_array[x][y];          //.5, .4, .3, .2, .1, .05
                }
                else if (elevation_array[x][y] > .8)
                {
                    baseTemps[x][y] -= .2 * elevation_array[x][y];
                }
                else if (elevation_array[x][y] > .7)
                {
                    baseTemps[x][y] -= .12 * elevation_array[x][y];
                }
                else if (elevation_array[x][y] > .6)
                {
                    baseTemps[x][y] -= .08 * elevation_array[x][y];
                }
                else if (elevation_array[x][y] > .5)
                {
                    baseTemps[x][y] -= .05 * elevation_array[x][y];
                }
                else if (elevation_array[x][y] > .4)
                {
                    baseTemps[x][y] -= .02 * elevation_array[x][y];
                }
            }


        }

    }
        //console.log(baseTemps);
        return baseTemps;


}


function generateBaseMoisture(elevation_array, temperature_array, width, height)
{
    var moisture_array = matrix(width, height, 0);

    for(var y = 0; y < height; y++)
    {
        
        for(var x = 0; x < width; x++)
        {
            

            if(elevation_array[x][y] < sea_level)
            {
                moisture_array[x][y] = temperature_array[x][y];
            }
            else
            {
                moisture_array[x][y] = 0;
            }




        }


    }

    return moisture_array;


}


function generateBaseWind(width, height)
{
    

    //generate "origin" points, representing pressure systems randomly at first, can add more realistic locations later
    //create circular patterns around these points, with randomized reach and intensity, consisting of a wind vector at each point
    //where these patterns overlap, calculate weighted average vector

    var wind_array = matrix(width, height, { xcomp: 0, ycomp: 0, originx: 0, originy: 0 });
    var wind_count_array = matrix(width, height, 0);
    var wind_origins = [];
    var origin_num = wind_cell_count; //var origin_num = 10; //number of wind origin points


    for(var i = 0; i < origin_num; i++)
    {
        
        wind_origins[i] = { x: Math.floor(Math.random() * (width - 1)), y: Math.floor(Math.random() * (height - 1)), intensity: Math.floor(Math.random() * 49) + 1, reach: Math.floor(Math.random() * (Math.sqrt((width*width) + (height*height)))/4), isCW: Math.floor(Math.random() * 2) };
        //console.log("Origin Point #" + i + " (" + wind_origins[i].x + ", " + wind_origins[i].y + ")");

        for(var r = 1; r <= wind_origins[i].reach; r++) //go through each "ring" of the circle
        {
            
              
              for(var p = -r; p <= r; p++)
              {
                  for(var q = -r; q <= r; q++)
                  {

                      var wrapp = wind_origins[i].x + p;
                      var wrapq = wind_origins[i].y + q;


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
                            wrapq = 0;
                            //wrapyu = height - y;
                        }
                        if (wrapq >= height)
                        {
                            wrapq = height - 1;
                            //wrapyb = y % height - 1;
                        } 
                      
                      if(Math.abs(p) == r || Math.abs(q) == r) //make sure the points are only in the "ring" not in the filled circle
                      {
                          //maybe add perlin noise to affect intensity at each point later
                          if(wind_origins[i].isCW == 1){
                              //wind_array[wind_origins[i].x + p][wind_array[i].y + q] += { xcomp: wind_origins[i].intensity * (-1 * q) / r , ycomp: wind_origins[i].intensity * p / r };
                              //wind_array[wind_origins[i].x + p][wind_origins[i].y + q] = { xcomp: wind_array[wind_origins[i].x + p][wind_origins[i].y + q].xcomp + wind_origins[i].intensity * (-1 * q) / r , ycomp: wind_array[wind_origins[i].x + p][wind_origins[i].y + q].ycomp + wind_origins[i].intensity * p / r };
                              //wind_array[wind_origins[i].x + p][wind_array[i].y + q] = { xcomp: (wind_origins[i].intensity * (-1 * q) / r)/ (wind_count_array[wind_origins[i].x + p][wind_array[i].y + q] + 1), ycomp: (wind_origins[i].intensity * p / r)/(wind_count_array[wind_origins[i].x + p][wind_array[i].y + q] + 1) };
                              //wind_count_array[wind_origins[i].x + p][wind_array[i].y + q]++;
                              
                 
                              wind_array[wrapp][wrapq] = { xcomp: wind_array[wrapp][wrapq].xcomp + (wind_origins[i].intensity * (-1 * q) / r ), ycomp: wind_array[wrapp][wrapq].ycomp + wind_origins[i].intensity * p / r, originx: wind_origins[i].x, originy: wind_origins[i].y};
                              wind_count_array[wrapp][wind_origins[i].y + q]++; 
                              // + (.05 * wind_origins[i].intensity * (2 * Math.random() - 1))      //variance factor
                          }
                          else
                          {
                              //wind_array[wind_origins[i].x + p][wind_array[i].y + q] += { xcomp: wind_origins[i].intensity * q / r , ycomp: wind_origins[i].intensity * (-1 * p) / r };
                              //wind_array[wind_origins[i].x + p][wind_array[i].y + q] = { xcomp: wind_array[wind_origins[i].x + p][wind_array[i].y + q].xcomp + wind_origins[i].intensity * q / r , ycomp: wind_array[wind_origins[i].x + p][wind_array[i].y + q].ycomp + wind_origins[i].intensity * (-1 * p) / r };
                              //wind_count_array[wind_origins[i].x + p][wind_array[i].y + q]++; 
                              
                              wind_array[wrapp][wrapq] = { xcomp: wind_array[wrapp][wrapq].xcomp + wind_origins[i].intensity * q / r , ycomp: wind_array[wrapp][wrapq].ycomp + wind_origins[i].intensity * (-1 * p) / r, originx: wind_origins[i].x, originy: wind_origins[i].y };
                              wind_count_array[wrapp][wrapq]++; 
                               
                          }


                      }
                   }


              }
              
                

        }
    
    
    }



    for(var y = 0; y < height; y++)
    {
        for(var x = 0; x < width; x++)
        {

            if (wind_array[x][y].xcomp == 0 && wind_array[x][y].ycomp == 0) {

                var tempIntensity = Math.floor(Math.random() * 50) - 25;
                wind_array[x][y] = { xcomp: wind_array[x][y].xcomp + .15 * tempIntensity, ycomp: wind_array[wrapp][wrapq].ycomp + .15 * tempIntensity, originx: wind_array[x][y].originx, originy: wind_array[x][y].originy };

            }
            else {
                wind_array[x][y] = { xcomp: wind_array[x][y].xcomp / wind_count_array[x][y], ycomp: wind_array[x][y].ycomp / wind_count_array[x][y] };
            }
        }
    }

    return wind_array;
}


function distributeMoisture(moisture_array, elevation_array, temperature_array, wind_array, width, height){


    var distributed_array = matrix(width, height, 0);
    var pn = new Perlin(Math.random());
    var pn2 = new Perlin(Math.random());

    for(var y = 0; y < height; y++){
        for( var x = 0; x < width;x++){

    //var x = 400;
    //var y = 200;

            var ny = 4*y / height;
            
            nx = Math.cos((x * 2 * Math.PI) / width); 
            nz = Math.sin((x * 2 * Math.PI) / width);

            
            var value = pn.noise(nx, ny, nz) + .5 * pn.noise(2 * nx, 2 * ny, 2*nz) + .25 * pn.noise(4 * nx, 4 * ny, 4*nz) + .125 * pn.noise(8 * nx, 8 * ny, 8*nz) + .0625 * pn.noise(16 * nx, 16 * ny, 16*nz);

            value /= 1.28;
            value = Math.pow(value, 2);

            //distributed_array[x][y] += .2*value;

            if (elevation_array[x][y] < sea_level) {
                //console.log("sea");
                var windspeed = Math.sqrt(wind_array[x][y].xcomp * wind_array[x][y].xcomp + wind_array[x][y].ycomp * wind_array[x][y].ycomp);
                var windxcomp = wind_array[x][y].xcomp;
                var windycomp = wind_array[x][y].ycomp;
                var moisture_remaining = moisture_array[x][y]*3;
                var current_temp = temperature_array[x][y];
                var current_elevation = elevation_array[x][y];

                var unitxcomp = windxcomp / windspeed;
                var unitycomp = windycomp / windspeed;

                //var xvec = x + Math.floor(windxcomp);
                //var yvec = y + Math.floor(windycomp);
                var xvec = x + Math.round(unitxcomp);
                var yvec = y + Math.round(unitycomp);


                while (moisture_remaining > .01) {

                    if(xvec >= width)
                    {
                        xvec = xvec % width; 
                    }
                    if(xvec < 0)
                    {
                        xvec = width + xvec;
                    }
                    if(yvec >= height || yvec < 0)
                    {
                        break;
                    }
                    //console.log("xvec " + xvec + "yvec " + yvec);
                    // need to account for wrap on xvec and yvec
                    distributed_array[xvec][yvec] += moisture_remaining * (1 / (windspeed + 7) + (.6 * elevation_array[xvec][yvec] / (15 * temperature_array[xvec][yvec] + .8)));   
                    //m * 1/(v + 7) + h/15t
                    //console.log("m_val " + distributed_array[xvec][yvec] + " m_remaining: "+moisture_remaining+" windspeed: "+windspeed+" elevation: "+elevation_array[xvec][yvec]+" temperature: "+temperature_array[xvec][yvec]);

                    //probably need to calculate the unit vector of the wind here - currently skipping a lot of pixels, only going to pixel where vector ends
                    /*windspeed = Math.sqrt(wind_array[xvec][yvec].xcomp * wind_array[xvec][yvec].xcomp + wind_array[xvec][yvec].ycomp * wind_array[xvec][yvec].ycomp);
                    xvec += wind_array[xvec][yvec].xcomp;
                    yvec += wind_array[xvec][yvec].ycomp;
                    moisture_remaining -= distributed_array[xvec][yvec];*/
                    moisture_remaining -= .05*distributed_array[xvec][yvec];

                    var unitxvec = Math.round(unitxcomp);
                    var unityvec = Math.round(unitycomp);

                    xvec += unitxvec;
                    yvec += unityvec;

                    if(xvec >= width)
                    {
                        xvec = xvec % width; 
                    }
                    if(xvec < 0)
                    {
                        xvec = width + xvec;
                    }
                    if(yvec >= height || yvec < 0)
                    {
                        break;
                    }

                    //console.log("xvec+unitxvec " + xvec + " yvec+unityvec " + yvec);

                    
                    var resultant_vecx = wind_array[xvec][yvec].xcomp + windxcomp;
                    var resultant_vecy = wind_array[xvec][yvec].ycomp + windycomp;

                    var resultant_mag = Math.sqrt(resultant_vecx * resultant_vecx + resultant_vecy * resultant_vecy);

                    unitxcomp = resultant_vecx / resultant_mag;
                    unitycomp = resultant_vecy / resultant_mag;

                    




                }
                  
            }
       }

    }
    distributed_array = averageArray(distributed_array, elevation_array, width, height, 10, true);

    for (var g = 0; g < height; g++ )
    {
        for(var h = 0; h < width; h++)
        {
            
            /*var ny = 4*g / height;
            
            nx = Math.cos((h * 2 * Math.PI) / width); 
            nz = Math.sin((h * 2 * Math.PI) / width);

            
            var value2 = pn2.noise(nx, ny, nz) + .5 * pn2.noise(2 * nx, 2 * ny, 2*nz) + .25 * pn2.noise(4 * nx, 4 * ny, 4*nz) + .125 * pn2.noise(8 * nx, 8 * ny, 8*nz) + .0625 * pn2.noise(16 * nx, 16 * ny, 16*nz);

            value2 /= 1.28;
            value2 = Math.pow(value2, 2);
            */


            if(elevation_array[h][g] < sea_level)
            {
                distributed_array[h][g] = 0;
            }
            /*else
            {
                distributed_array[h][g] *= value2;


            }*/

        }
    }

    //distributed_array = averageArray(distributed_array, elevation_array, width, height);

        return distributed_array;
}

function distributeMoisture2(moisture_array, elevation_array, temperature_array, wind_array, width, height){

    //console.log("in-function");

    var count = 0;

    var distributed_array = matrix(width, height, 0);
    var averaging_array = matrix(width, height, 0);
    //var pn = new Perlin(PerlinSeeds[5]);
    //var pn2 = new Perlin(PerlinSeeds[6]);

    var pn = new Perlin(Math.random());
    var pn2 = new Perlin(Math.random());

    var windspeed = 0;
    var windxcomp = 0;
    var windycomp = 0;
    var moisture_remaining = 0;
    var current_temp = 0;
    var last_elevation = 0;

    var unitxcomp = 0;
    var unitycomp = 0;

    //var xvec = x + Math.floor(windxcomp);
    //var yvec = y + Math.floor(windycomp);
    var xvec = 0;
    var yvec = 0;

    var unitxvec = 0;
    var unityvec = 0;

    var resultant_vecx = 0;
    var resultant_vecy = 0;

    var resultant_mag = 0;

    var el_slope = 0;

    var ny = 0;
    var nx = 0;
    var nz = 0;
    
    var value = 0;

    //var lastvecx = 0;
    //var lastvecy = 0;
    var progresspercent = 0;
    for(var y = 0; y < height; y++){
        for( var x = 0; x < width;x++){

    //var x = 799;
    //var y = 199;
            //console.log("in for loop " + x + ", " + y);
            //setTimeout(distributeMoistureLoop, 0);
            //distributeMoistureLoop();
            
                   
            //function distributeMoistureLoop(){
            ny = 4*y / height;
            
            nx = Math.cos((x * 2 * Math.PI) / width); 
            nz = Math.sin((x * 2 * Math.PI) / width);

            
            value = pn.noise(nx, ny, nz) + .5 * pn.noise(2 * nx, 2 * ny, 2*nz) + .25 * pn.noise(4 * nx, 4 * ny, 4*nz) + .125 * pn.noise(8 * nx, 8 * ny, 8*nz) + .0625 * pn.noise(16 * nx, 16 * ny, 16*nz);

            value /= 1.28;
            value = Math.pow(value, 2);

            if (elevation_array[x][y] >= sea_level)
            {
                distributed_array[x][y] += .15 * value;
            }

            if (elevation_array[x][y] < sea_level) {
                //console.log("sea");
                windspeed = Math.sqrt(wind_array[x][y].xcomp * wind_array[x][y].xcomp + wind_array[x][y].ycomp * wind_array[x][y].ycomp);
                windxcomp = wind_array[x][y].xcomp;
                windycomp = wind_array[x][y].ycomp;
                moisture_remaining = moisture_array[x][y]*50;
                current_temp = temperature_array[x][y];
                last_elevation = elevation_array[x][y];

                unitxcomp = windxcomp / windspeed;
                unitycomp = windycomp / windspeed;

                //var xvec = x + Math.floor(windxcomp);
                //var yvec = y + Math.floor(windycomp);
                xvec = x + Math.round(unitxcomp);
                yvec = y + Math.round(unitycomp);

                unitxvec = 0;
                unityvec = 0;

                resultant_vecx = 0;
                resultant_vecy = 0;

                el_slope = 0;

                //console.log("pre while loop xvec: " + xvec + " yvec: " + yvec + " moisture_remaining: "+moisture_remaining + " moisture[x][y]: "+moisture_array[x][y] + "elevation: "+elevation_array[x][y]);

                while (moisture_remaining > .1) {
                    //console.log("in the while loop");
                    if(xvec >= width)
                    {
                        //console.log("Xwrap+");
                        xvec = xvec % width; 
                    }
                    if(xvec < 0)
                    {
                        //console.log("Xwrap-");
                        xvec = width + xvec;
                    }
                    if(yvec >= height || yvec < 0)
                    {   
                        //console.log("broken");
                        break;
                        
                    }

                    //if (isNaN(xvec) || isNaN(yvec))
                    //{

                    //if (yvec == 0 || yvec == 1)
                    //{
                    //    console.log("xvec " + xvec + "yvec " + yvec);
                    //}
                    //}
                    // need to account for wrap on xvec and yvec
                    //distributed_array[xvec][yvec] += moisture_remaining * (1 / (windspeed + 7) + (.6 * elevation_array[xvec][yvec] / (15 * temperature_array[xvec][yvec] + .8)));

                    if (last_elevation >= sea_level){ //&& elevation_array[xvec][yvec] >= .35)
                        
                        
                        
                        //{
                        //el_slope = (elevation_array[xvec][yvec] - last_elevation);
                        //el_slope = (.599 * (elevation_array[xvec][yvec] - last_elevation)) - (.2 * temperature_array[xvec][yvec]) + (.2 * elevation_array[xvec][yvec]) - (.001 * windspeed);
                        
                        
                        el_slope = (elevation_array[xvec][yvec] - last_elevation)*(Math.sqrt(elevation_array[xvec][yvec]) - (.5 * temperature_array[xvec][yvec]) - (.005 * windspeed) + .7);
                        //el_slope = elevation_array[xvec][yvec] - temperature_array[xvec][yvec] - .005 * windspeed + 1;
                        
                        
                        //console.log("ElSlope: " + el_slope + " Slope: "+ (elevation_array[xvec][yvec] - last_elevation) + " Temp: " + temperature_array[xvec][yvec] + " El: "+elevation_array[xvec][yvec] + " WS: "+windspeed);
                            //if (elevation_array[xvec][yvec] >= .35)
                            //{
                        //if (el_slope < .001)
                        //{
                        //    el_slope = .001;
                        //}
                       
                    }    
                    
                    
                    
                    else
                    {
                        //el_slope = .01*(elevation_array[xvec][yvec] - last_elevation);
                        
                        
                        
                        el_slope = .01*(elevation_array[xvec][yvec] - last_elevation)*(Math.sqrt(elevation_array[xvec][yvec]) - (.5 * temperature_array[xvec][yvec]) - (.005 * windspeed) + .7);
                    }    
                    
                    
                    
                    //if (el_slope < .002){               //UNCOMMENT HERE
                    //    el_slope = .002;                //UNCOMMENT HERE
                    //}                                   //UNCOMMENT HERE

                    if(el_slope <= .002)
                    {
                        el_slope = .002;
                        //el_slope = Math.sqrt(elevation_array[xvec][yvec]) - (.5 * temperature_array[xvec][yvec]) - (.005 * windspeed) + .7;
                        //console.log("negative elsope: "+el_slope);
                    }
                    
                        
                   /*if (elevation_array[xvec][yvec] < .35)
                    {
                        var seaflag = true;
                        
                     }
                     else
                     {
                         var seaflag = false;

                     }   */
                        
                        //el_slope *= 1.54; //convert range from 0-.65 to 0-1
                   // }
                   // else
                  // {
                   //     el_slope = 0;
                   // }

                   //if(elevation_array[xvec][yvec] < .35)
                   //{
                       //el_slope = 0;
                       //distributed_array[xvec][yvec] = 0;
                       //console.log("el < .35");
                  //}
                  // else
                  // {
                      // distributed_array[xvec][yvec] += moisture_remaining * el_slope;
                      // console.log("el > .35");
                  // }


                    distributed_array[xvec][yvec] += moisture_remaining * el_slope;
                    //averaging_array[xvec][yvec] += 1;
                    //distributed_array[xvec][yvec] += el_slope;
                    
                    
                    //distributed_array[xvec][yvec] += (.4 * moisture_remaining * el_slope * el_slope);     //TEST WITH THIS ONE !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                    //if (xvec < 100 && yvec < 100)
                    //{
                    //console.log("distributed arr: " + distributed_array[xvec][yvec] + " mremaining: " + moisture_remaining + " curr el: " + elevation_array[xvec][yvec] + " last el: " + last_elevation);
                    //}
                    
                    //if(elevation_array[xvec][yvec] < .35)
                    //{
                    //    distributed_array[xvec][yvec] = .001;
                    //}
                    
                      
                    //m * 1/(v + 7) + h/15t
                    //console.log("m_val " + distributed_array[xvec][yvec] + " m_remaining: "+moisture_remaining+" windspeed: "+windspeed+" elevation: "+elevation_array[xvec][yvec]+" temperature: "+temperature_array[xvec][yvec]);

                    //probably need to calculate the unit vector of the wind here - currently skipping a lot of pixels, only going to pixel where vector ends
                    /*windspeed = Math.sqrt(wind_array[xvec][yvec].xcomp * wind_array[xvec][yvec].xcomp + wind_array[xvec][yvec].ycomp * wind_array[xvec][yvec].ycomp);
                    xvec += wind_array[xvec][yvec].xcomp;
                    yvec += wind_array[xvec][yvec].ycomp;
                    moisture_remaining -= distributed_array[xvec][yvec];*/
                    //moisture_remaining -= distributed_array[xvec][yvec];

                    

                     //console.log(seaflag);
                     
                    if (elevation_array[xvec][yvec] < sea_level){

                        //var tester = .0001;
                    //    console.log("in the statement");
                        //moisture_remaining -= distributed_array[xvec][yvec];
                       // distributed_array[xvec][yvec] = tester;
                    //    distributed_array[xvec][yvec] = 0;
                        distributed_array[xvec][yvec] = .0001;
                    }


                    if (elevation_array[xvec][yvec] > .6)
                    {

                        moisture_remaining -= 4*distributed_array[xvec][yvec];
                    }
                    else
                    {
                        moisture_remaining -= distributed_array[xvec][yvec];
                    }


                    last_elevation = elevation_array[xvec][yvec];

                    //console.log("unitxcomp: "+unitxcomp+" unitycomp: "+unitycomp);
                    //lastvecx = xvec;
                    //lastvecy = yvec;

                    unitxvec = Math.round(unitxcomp);
                    unityvec = Math.round(unitycomp);

                    xvec += unitxvec;
                    yvec += unityvec;

                    //console.log("testetstestsetsetsetet");

                    if(xvec >= width)
                    {
                        xvec = xvec % width; 
                    }
                    if(xvec < 0)
                    {
                        xvec = width + xvec;
                    }
                    if(yvec >= height || yvec < 0)
                    {   
                        //console.log("broken2");
                        break;
                        //console.log("broken2");
                    }

                    //if(Number.isNaN(wind_array[xvec][yvec].xvec))
                    //{
                    //    console.log("WArrayx: "+wind_array[xvec][yvec].xvec+" xvec: "+xvec+" yvec: "+yvec);
                    //}

                    //console.log("testtesttest");

                    //console.log("xvec+unitxvec " + xvec + " yvec+unityvec " + yvec);
                    //console.log("xvec+unitxvec " + xvec + " yvec+unityvec " + yvec + " |||| unitxvec: "+ unitxvec + " unityvec: "+unityvec+" |||| unitxcomp: "+unitxcomp+" unitycomp: "+unitycomp);
                    //console.log("unitxcomp: "+unitxcomp+" unitycomp: "+unitycomp);

                    //if(isNaN(xvec))
                    //{
                        //console.log(wind_array);
                    //    console.log("last xvec " + lastvecx + " last yvec" + lastvecy);
                    //    console.log("resmag: " + resultant_mag + " resvecx: " + resultant_vecx + " resvecy: " + resultant_vecy);
                    //}
                    //console.log("wind_array[xvec][yvec].xcomp: " + wind_array[xvec][yvec].xcomp + " windxcomp: " + windxcomp);
                    resultant_vecx = wind_array[xvec][yvec].xcomp + windxcomp;
                    resultant_vecy = wind_array[xvec][yvec].ycomp + windycomp;
                    //console.log(resultant_vecx);
                    
                    resultant_mag = Math.sqrt(resultant_vecx * resultant_vecx + resultant_vecy * resultant_vecy);

                    if (resultant_mag != 0)
                    {
                        unitxcomp = resultant_vecx / resultant_mag;
                        unitycomp = resultant_vecy / resultant_mag;
                    }
                    else
                    {
                        //console.log("/0 caught");
                        //unitxcomp = 0;
                        //unitycomp = 0;
                        break; 
                    }
                    

                    




                }
                  
            }



            //count++;
            //document.getElementById("ProgressPercent").innerHTML = count.toString();
            //}

            progresspercent += 1/(height*width);
            //drawLoadBar(6 / 9 + (progresspercent/12));

       }    //FOR LOOP EDGE

    }        //     FOR LOOP EDGE
    distributed_array = averageArray(distributed_array, elevation_array, width, height, 10, true);

    for (var g = 0; g < height; g++ )               //uncomment here
    {
        for(var h = 0; h < width; h++)
        {
            
            /*var ny = 4*g / height;
            
            nx = Math.cos((h * 2 * Math.PI) / width); 
            nz = Math.sin((h * 2 * Math.PI) / width);

            
            var value2 = pn2.noise(nx, ny, nz) + .5 * pn2.noise(2 * nx, 2 * ny, 2*nz) + .25 * pn2.noise(4 * nx, 4 * ny, 4*nz) + .125 * pn2.noise(8 * nx, 8 * ny, 8*nz) + .0625 * pn2.noise(16 * nx, 16 * ny, 16*nz);

            value2 /= 1.28;
            value2 = Math.pow(value2, 2);
            */


            if(elevation_array[h][g] < sea_level)   //uncomment here
            {
                distributed_array[h][g] = 0;
            }
            //else if(averaging_array[h][g] != 0)               //If only adding el_slope and not using el_slope*m_remaining, average
            //{
            //   distributed_array[h][g] = distributed_array[h][g] / averaging_array[h][g];
            //}
            /*else
            {
                distributed_array[h][g] *= value2;
            

            }*/

       } //uncomment here
  }  //uncomment here

    //distributed_array = averageArray(distributed_array, elevation_array, width, height);

        return distributed_array;
}


function distributeMoisture3(moisture_array, elevation_array, temperature_array, wind_array, width, height){

    //console.log("in-function");

    var count = 0;

    var distributed_array = matrix(width, height, 0);
    var averaging_array = matrix(width, height, 0);
    var pn = new Perlin(Math.random());
    var pn2 = new Perlin(Math.random());

    var windspeed = 0;
    var windxcomp = 0;
    var windycomp = 0;
    var moisture_remaining = 0;
    var current_temp = 0;
    var last_elevation = 0;

    var unitxcomp = 0;
    var unitycomp = 0;

    //var xvec = x + Math.floor(windxcomp);
    //var yvec = y + Math.floor(windycomp);
    var xvec = 0;
    var yvec = 0;

    var unitxvec = 0;
    var unityvec = 0;

    var resultant_vecx = 0;
    var resultant_vecy = 0;

    var resultant_mag = 0;

    var el_slope = 0;

    var ny = 0;
    var nx = 0;
    var nz = 0;
    
    var value = 0;

    //var lastvecx = 0;
    //var lastvecy = 0;

    for(var y = 0; y < height; y++){
        for( var x = 0; x < width;x++){

    //var x = 799;
    //var y = 199;
            //console.log("in for loop " + x + ", " + y);        

            ny = 4*y / height;
            
            nx = Math.cos((x * 2 * Math.PI) / width); 
            nz = Math.sin((x * 2 * Math.PI) / width);

            
            value = pn.noise(nx, ny, nz) + .5 * pn.noise(2 * nx, 2 * ny, 2*nz) + .25 * pn.noise(4 * nx, 4 * ny, 4*nz) + .125 * pn.noise(8 * nx, 8 * ny, 8*nz) + .0625 * pn.noise(16 * nx, 16 * ny, 16*nz);

            value /= 1.28;
            value = Math.pow(value, 2);

            if (elevation_array[x][y] >= sea_level)
            {
                distributed_array[x][y] += .15 * value;
            }

            if (elevation_array[x][y] < sea_level) {
                //console.log("sea");
                windspeed = Math.sqrt(wind_array[x][y].xcomp * wind_array[x][y].xcomp + wind_array[x][y].ycomp * wind_array[x][y].ycomp);
                windxcomp = wind_array[x][y].xcomp;
                windycomp = wind_array[x][y].ycomp;
                moisture_remaining = moisture_array[x][y]*25;
                current_temp = temperature_array[x][y];
                //last_elevation = elevation_array[x][y];

                unitxcomp = windxcomp / windspeed;
                unitycomp = windycomp / windspeed;

                //var xvec = x + Math.floor(windxcomp);
                //var yvec = y + Math.floor(windycomp);
                xvec = x + Math.round(unitxcomp);
                yvec = y + Math.round(unitycomp);

                unitxvec = 0;
                unityvec = 0;

                resultant_vecx = 0;
                resultant_vecy = 0;

                el_slope = 0;

                //console.log("pre while loop xvec: " + xvec + " yvec: " + yvec + " moisture_remaining: "+moisture_remaining + " moisture[x][y]: "+moisture_array[x][y] + "elevation: "+elevation_array[x][y]);

                while (moisture_remaining > 1) {
                    //console.log("in the while loop");
                    if(xvec >= width)
                    {
                        //console.log("Xwrap+");
                        xvec = xvec % width; 
                    }
                    if(xvec < 0)
                    {
                        //console.log("Xwrap-");
                        xvec = width + xvec;
                    }
                    if(yvec >= height || yvec < 0)
                    {   
                        //console.log("broken");
                        break;
                        
                    }

                    //if (isNaN(xvec) || isNaN(yvec))
                    //{

                    //if (yvec == 0 || yvec == 1)
                    //{
                    //    console.log("xvec " + xvec + "yvec " + yvec);
                    //}
                    //}
                    // need to account for wrap on xvec and yvec
                    //distributed_array[xvec][yvec] += moisture_remaining * (1 / (windspeed + 7) + (.6 * elevation_array[xvec][yvec] / (15 * temperature_array[xvec][yvec] + .8)));

                    //if (last_elevation >= .35){ //&& elevation_array[xvec][yvec] >= .35)
                        
                        
                        
                        //{
                        //el_slope = (elevation_array[xvec][yvec] - last_elevation);
                        //el_slope = (.599 * (elevation_array[xvec][yvec] - last_elevation)) - (.2 * temperature_array[xvec][yvec]) + (.2 * elevation_array[xvec][yvec]) - (.001 * windspeed);
                        
                        
                        //el_slope = (elevation_array[xvec][yvec] - last_elevation)*(Math.sqrt(elevation_array[xvec][yvec]) - (.5 * temperature_array[xvec][yvec]) - (.005 * windspeed) + .7);
                        
                        
                        el_slope = .5*(elevation_array[xvec][yvec] * elevation_array[xvec][yvec]) - Math.sqrt((temperature_array[xvec][yvec]+.6)/1.9) - .005 * windspeed + 1.25;
                        //el_slope = 1 - ((temperature_array[xvec][yvec] + .5) / 1.8);
                        //el_slope = .5*temperature_array[xvec][yvec];
                         
                         //convert to 0-1 range (with a few outliers)
                        //el_slope = (el_slope - .2) / 1.6;  //MAYBE REMOVE?
                        
                        //console.log("ElSlope: " + el_slope + " Slope: "+ (elevation_array[xvec][yvec] - last_elevation) + " Temp: " + temperature_array[xvec][yvec] + " El: "+elevation_array[xvec][yvec] + " WS: "+windspeed);
                            //if (elevation_array[xvec][yvec] >= .35)
                            //{
                        //if (el_slope < .001)
                        //{
                        //    el_slope = .001;
                        //}
                       
                   // }    
                    
                    
                    
                   // else
                   // {
                        //el_slope = .01*(elevation_array[xvec][yvec] - last_elevation);
                        
                        
                        
                    //    el_slope = .01*(elevation_array[xvec][yvec] - last_elevation)*(Math.sqrt(elevation_array[xvec][yvec]) - (.5 * temperature_array[xvec][yvec]) - (.005 * windspeed) + .7);
                    //}    
                    
                    
                    
                    //if (el_slope < .002){               //UNCOMMENT HERE
                    //    el_slope = .002;                //UNCOMMENT HERE
                    //}                                   //UNCOMMENT HERE

                    //if(el_slope <= .002)
                    //{
                    //    el_slope = .002;
                        //el_slope = Math.sqrt(elevation_array[xvec][yvec]) - (.5 * temperature_array[xvec][yvec]) - (.005 * windspeed) + .7;
                        //console.log("negative elsope: "+el_slope);
                    //}
                    
                        
                   /*if (elevation_array[xvec][yvec] < .35)
                    {
                        var seaflag = true;
                        
                     }
                     else
                     {
                         var seaflag = false;

                     }   */
                        
                        //el_slope *= 1.54; //convert range from 0-.65 to 0-1
                   // }
                   // else
                  // {
                   //     el_slope = 0;
                   // }

                   //if(elevation_array[xvec][yvec] < .35)
                   //{
                       //el_slope = 0;
                       //distributed_array[xvec][yvec] = 0;
                       //console.log("el < .35");
                  //}
                  // else
                  // {
                      // distributed_array[xvec][yvec] += moisture_remaining * el_slope;
                      // console.log("el > .35");
                  // }


                    distributed_array[xvec][yvec] += moisture_remaining * el_slope;
                    //distributed_array[xvec][yvec] += el_slope;
                    //averaging_array[xvec][yvec] += 1;
                    
                    //distributed_array[xvec][yvec] += (.4 * moisture_remaining * el_slope * el_slope);     //TEST WITH THIS ONE !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                    //if (xvec < 100 && yvec < 100)
                    //{
                    //console.log("distributed arr: " + distributed_array[xvec][yvec] + " mremaining: " + moisture_remaining + " curr el: " + elevation_array[xvec][yvec] + " last el: " + last_elevation);
                    //}
                    
                    //if(elevation_array[xvec][yvec] < .35)
                    //{
                    //    distributed_array[xvec][yvec] = .001;
                    //}
                    
                      
                    //m * 1/(v + 7) + h/15t
                    //console.log("m_val " + distributed_array[xvec][yvec] + " m_remaining: "+moisture_remaining+" windspeed: "+windspeed+" elevation: "+elevation_array[xvec][yvec]+" temperature: "+temperature_array[xvec][yvec]);

                    //probably need to calculate the unit vector of the wind here - currently skipping a lot of pixels, only going to pixel where vector ends
                    /*windspeed = Math.sqrt(wind_array[xvec][yvec].xcomp * wind_array[xvec][yvec].xcomp + wind_array[xvec][yvec].ycomp * wind_array[xvec][yvec].ycomp);
                    xvec += wind_array[xvec][yvec].xcomp;
                    yvec += wind_array[xvec][yvec].ycomp;
                    moisture_remaining -= distributed_array[xvec][yvec];*/
                    //moisture_remaining -= distributed_array[xvec][yvec];

                    

                     //console.log(seaflag);
                     
                    if (elevation_array[xvec][yvec] < sea_level){

                        distributed_array[xvec][yvec] = .0001;
                    }

                    moisture_remaining -= .1*distributed_array[xvec][yvec];

                    //last_elevation = elevation_array[xvec][yvec];

                    //console.log("unitxcomp: "+unitxcomp+" unitycomp: "+unitycomp);
                    //lastvecx = xvec;
                    //lastvecy = yvec;

                    unitxvec = Math.round(unitxcomp);
                    unityvec = Math.round(unitycomp);

                    xvec += unitxvec;
                    yvec += unityvec;

                    //console.log("testetstestsetsetsetet");

                    if(xvec >= width)
                    {
                        xvec = xvec % width; 
                    }
                    if(xvec < 0)
                    {
                        xvec = width + xvec;
                    }
                    if(yvec >= height || yvec < 0)
                    {   
                        //console.log("broken2");
                        break;
                        //console.log("broken2");
                    }

                    //if(Number.isNaN(wind_array[xvec][yvec].xvec))
                    //{
                    //    console.log("WArrayx: "+wind_array[xvec][yvec].xvec+" xvec: "+xvec+" yvec: "+yvec);
                    //}

                    //console.log("testtesttest");

                    //console.log("xvec+unitxvec " + xvec + " yvec+unityvec " + yvec);
                    //console.log("xvec+unitxvec " + xvec + " yvec+unityvec " + yvec + " |||| unitxvec: "+ unitxvec + " unityvec: "+unityvec+" |||| unitxcomp: "+unitxcomp+" unitycomp: "+unitycomp);
                    //console.log("unitxcomp: "+unitxcomp+" unitycomp: "+unitycomp);

                    //if(isNaN(xvec))
                    //{
                        //console.log(wind_array);
                    //    console.log("last xvec " + lastvecx + " last yvec" + lastvecy);
                    //    console.log("resmag: " + resultant_mag + " resvecx: " + resultant_vecx + " resvecy: " + resultant_vecy);
                    //}
                    //console.log("wind_array[xvec][yvec].xcomp: " + wind_array[xvec][yvec].xcomp + " windxcomp: " + windxcomp);
                    resultant_vecx = wind_array[xvec][yvec].xcomp + windxcomp;
                    resultant_vecy = wind_array[xvec][yvec].ycomp + windycomp;
                    //console.log(resultant_vecx);
                    
                    resultant_mag = Math.sqrt(resultant_vecx * resultant_vecx + resultant_vecy * resultant_vecy);

                    if (resultant_mag != 0)
                    {
                        unitxcomp = resultant_vecx / resultant_mag;
                        unitycomp = resultant_vecy / resultant_mag;
                    }
                    else
                    {
                        //console.log("/0 caught");
                        //unitxcomp = 0;
                        //unitycomp = 0;
                        break; 
                    }
                    

                    




                }
                  
            }

            //count++;
            //document.getElementById("ProgressPercent").innerHTML = count.toString();

       }    //FOR LOOP EDGE

    }        //     FOR LOOP EDGE
    distributed_array = averageArray(distributed_array, elevation_array, width, height, 12, true);

    for (var g = 0; g < height; g++ )               //uncomment here
    {
        for(var h = 0; h < width; h++)
        {
            
            /*var ny = 4*g / height;
            
            nx = Math.cos((h * 2 * Math.PI) / width); 
            nz = Math.sin((h * 2 * Math.PI) / width);

            
            var value2 = pn2.noise(nx, ny, nz) + .5 * pn2.noise(2 * nx, 2 * ny, 2*nz) + .25 * pn2.noise(4 * nx, 4 * ny, 4*nz) + .125 * pn2.noise(8 * nx, 8 * ny, 8*nz) + .0625 * pn2.noise(16 * nx, 16 * ny, 16*nz);

            value2 /= 1.28;
            value2 = Math.pow(value2, 2);
            */


            if(elevation_array[h][g] < sea_level)   //uncomment here
            {
                distributed_array[h][g] = 0;
            }
            //else if(averaging_array[h][g] != 0)               //If only adding el_slope and not using el_slope*m_remaining, average
            //{
             //   distributed_array[h][g] = distributed_array[h][g] / averaging_array[h][g];
            //}


            /*else
            {
                distributed_array[h][g] *= value2;


            }*/

       } //uncomment here
  }  //uncomment here

    //distributed_array = averageArray(distributed_array, elevation_array, width, height);

        return distributed_array;
}

function generateBiomes(elevation_array, moisture_array, temperature_array, width, height)
{
    //console.log("special new flag");
    //console.log("reached generate");
    //window.alert("got here");
    var biome_array = matrix(width, height, "");
    //var river_test = 0;

    for (var y = 0; y < height; y++ )
    {
        for(var x = 0; x < width; x++)
        {

            //console.log("y: " + y + "x: "+x);
          
          if(temperature_array[x][y] < .03 && elevation_array[x][y] <.65)
          {
              biome_array[x][y] = "ICE";
              continue;
          }
          
          //GENERATE RIVER SOURCES
          //console.log(genRivers);
          if (genRivers == "true")
          {
              if (elevation_array[x][y] > .55 && moisture_array[x][y] > .15 && Math.random() < .0035)
              {
                  //----------OLD RIVER CODE------------------
                  //biome_array[x][y] = "RIVER";
                  //biome_array = defineRivers2(biome_array, elevation_array, x, y, width, height);
                  //continue;
                  //--------------------------------------------


                  RiverLayer[x][y] = Math.random() * .9 + .1; //Math.random() * .00009 + .00001;
                  defineRivers3(biome_array, elevation_array, x, y, width, height);

              }
          }
          
          //SET OCEANS

          if (elevation_array[x][y] < (.5714*sea_level)) //0.2)
          {
            biome_array[x][y] = "OCEAN";
            continue;  
          } 
          if (elevation_array[x][y] < sea_level)
          {
            biome_array[x][y] = "SHALLOW OCEAN";
            continue;  
          } 
          if (elevation_array[x][y] < (sea_level+.027)) //0.377)
          {
            biome_array[x][y] = "COASTLAND";
            continue;    
          } 
  
          //SET MOUNTAINS

          if(elevation_array[x][y] >= .68)
          {
              if(temperature_array[x][y] > .2)
              {
                  biome_array[x][y] = "ROCKY MOUNTAIN";
                  continue;
              }
              else
              {
                  biome_array[x][y] = "SNOWY MOUNTAIN";
                  continue;
              }


          }

          

          //TROPICAL BAND/RED TEMP

          if (temperature_array[x][y] > 0.6) {
            if (moisture_array[x][y] < 0.15)
            {
                biome_array[x][y] = "TROPICAL DESERT";
                continue;
            }
            if (moisture_array[x][y] < sea_level)
            {
                biome_array[x][y] = "SAVANNAH";
                continue;
            }
            if (moisture_array[x][y] < 0.5)
            {
                biome_array[x][y] = "SHRUBLAND";
                continue;
            }
            if (moisture_array[x][y] < 0.75)
            {
                biome_array[x][y] = "TROPICAL SEASONAL FOREST";
                continue;
            }
            if (moisture_array[x][y] >= 0.75)
            {
                biome_array[x][y] = "TROPICAL RAIN FOREST";
                continue;
            }
            
          }

          //TEMPERATE BAND/YELLOW + ORANGE TEMP

          if (temperature_array[x][y] > 0.25) {
            if (moisture_array[x][y] < 0.15)
            {
                biome_array[x][y] = "TEMPERATE DESERT";
                continue;
            }
            if (moisture_array[x][y] < 0.2)
            {
                biome_array[x][y] = "STEPPE";
                continue;
            }
            if (moisture_array[x][y] < 0.4)
            {
                biome_array[x][y] = "GRASSLAND";
                continue;
            }
            if (moisture_array[x][y] < 0.5)
            {
                biome_array[x][y] = "CHAPARRAL";
                continue;
            }
            if (moisture_array[x][y] < 0.85)
            {
                biome_array[x][y] = "TEMPERATE SEASONAL FOREST";
                continue;
            }
            if (moisture_array[x][y] >= 0.85)
            {
                biome_array[x][y] = "TEMPERATE RAIN FOREST";
                continue;
            }
            
          }


          //SUBPOLAR + POLAR BAND/GREEN + TEAL + BLUE TEMP
          if (temperature_array[x][y] > 0.05) {
            if (moisture_array[x][y] < 0.2)
            {
                biome_array[x][y] = "TUNDRA";
                continue;
            }
            if (moisture_array[x][y] < 0.55)
            {
                biome_array[x][y] = "TAIGA";
                continue;
            }
            if (moisture_array[x][y] >= 0.55)
            {
                biome_array[x][y] = "BOREAL FOREST";
                continue;
            }
          }
          
          //ICECAP
          if(moisture_array[x][y] < .1)
          {
              biome_array[x][y] = "TUNDRA";
              continue;
          }
          if(moisture_array[x][y] >= .1)
          {
              biome_array[x][y] = "ICE";
              continue;
          }  
          
          

        }
    }
    //console.log(biome_array);
    return biome_array;
  
}

function defineRivers(biome_array, elevation_array, width, height)
{
    //console.log("Rivers Function");
    var river_array = matrix(width, height, "");
    
    var current_el = 0;

    var wrapx = 0;
    var wrapy = 0;
                
    var smallx = 0;
    var smally = 0;

    var count = 0;

    lowxpos = 0;
    lowypos = 0;
    
    river_array = biome_array;    
     
    for(var y = 0; y < height; y++)
    {
        for(var x = 0; x < width; x++)
        {
            //console.log("x: " + x + " y: " + y);
            if(biome_array[x][y] == "RIVER")
            {

                //console.log("Identified coordinate as river source");
                current_el = elevation_array[x][y];
                
                wrapx = 0;
                wrapy = 0;
                
                smallx = x;
                smally = y;

                //current_el = 2;  //DEBUG
                console.log("current_el prewhile loop: " + current_el);

                while(current_el >= sea_level)
                {
                   console.log("x: " + x + " y: " + y);
                   count++;
                   var smallest_el = Infinity;



                   console.log("curel inside while loop pre for loop "+current_el);
                    
                   for(var p = -1; p <= 1; p++)   //p = x axis
                   {
                      for(var q = -1; q <= 1; q++)    //q = y axis
                       {
                           
                           if(p == 0 && q == 0)        //Don't test current coordinate
                           {
                               continue;
                           }

                          
                           
                           wrapy = smally + q;

                           if(wrapy >= height || wrapy < 0)     //set up wrapping parameters
                           {
                               continue;
                           }


                           wrapx = smallx + p;

                           if(wrapx >= width)
                           {
                               wrapx = wrapx % width;
                           }
                           if(wrapx < 0)
                           {
                               wrapx = width + p;
                           }


                           //console.log("wx: " + wrapx + " wy: " + wrapy);

                           if(biome_array[wrapx][wrapy] == "RIVER")
                           {
                               continue; 
                           }
                           
                           if(elevation_array[wrapx][wrapy] < smallest_el)
                           {
                               smallest_el = elevation_array[wrapx][wrapy];
                               lowxpos = wrapx;
                               lowypos = wrapy;
                               console.log("lowx: " + lowxpos + " lowy: " + lowypos + " small_el: " + smallest_el);
                           }
                           
                       }
                   }
                   
                   smallx = lowxpos;
                   smally = lowypos;

                   river_array[smallx][smally] = "RIVER";
                   current_el = smallest_el;    //DEBUG
                   console.log("curel after setting to smallel "+current_el);

                   current_el = .34; //DEBUG
                   console.log("curel after setting to .34 " + current_el);

                }
            }








        }
    }

    console.log("While Count: " + count);
    return river_array;


}


function defineRivers2(biome_array, elevation_array, x, y, width, height)
{
    //console.log("Rivers Function");
    var river_array = matrix(width, height, "");
    
    var current_el = 0;

    var wrapx = 0;
    var wrapy = 0;
                
    var smallx = 0;
    var smally = 0;

    var count = 0;

    var lowxpos = 0;
    var lowypos = 0;

    var lowcount = 0;
    
    river_array = biome_array;
    //console.log("got to function");
   
            if(biome_array[x][y] == "RIVER")
            {

                //console.log("Identified coordinate as river source");
                current_el = elevation_array[x][y];
                
                wrapx = 0;
                wrapy = 0;
                
                smallx = x;
                smally = y;

                //current_el = 2;  //DEBUG
                //console.log("current_el prewhile loop: " + current_el);

                while(current_el >= sea_level)
                {
                   //console.log("x: " + x + " y: " + y);
                   count++;
                   var smallest_el = current_el;
                   var next_smallest_el = Infinity;
                   lowcount = 0;


                   //console.log("curel inside while loop pre for loop "+current_el);
                    
                   for(var p = -1; p <= 1; p++)   //p = x axis
                   {
                      for(var q = -1; q <= 1; q++)    //q = y axis
                       {
                           
                           if(p == 0 && q == 0)        //Don't test current coordinate
                           {
                               continue;
                           }

                          
                           
                           wrapy = smally + q;

                           if(wrapy >= height || wrapy < 0)     //set up wrapping parameters
                           {
                               continue;
                           }


                           wrapx = smallx + p;

                           if(wrapx >= width)
                           {
                               wrapx = wrapx % width;
                           }
                           if(wrapx < 0)
                           {
                               wrapx = width + p;
                           }

                           
                           //console.log("wx: " + wrapx + " wy: " + wrapy);

                           if(biome_array[wrapx][wrapy] == "RIVER")
                           {
                               continue; 
                           }
                           
                           if(elevation_array[wrapx][wrapy] < smallest_el)
                           {
                               smallest_el = elevation_array[wrapx][wrapy];
                               lowxpos = wrapx;
                               lowypos = wrapy;
                               lowcount++;
                               //console.log("lowx: " + lowxpos + " lowy: " + lowypos + " small_el: " + smallest_el);
                           }
                           else if(elevation_array[wrapx][wrapy] < next_smallest_el)
                           {
                               next_smallest_el = elevation_array[wrapx][wrapy];

                           }
                           
                       }
                   }
                   
                   

                   river_array[lowxpos][lowypos] = "RIVER";
                   //console.log("slope to next point: " + (elevation_array[smallx][smally] - elevation_array[lowxpos][lowypos]));
                   //current_el = smallest_el;    //DEBUG
                   //console.log("curel after setting to smallel "+current_el);

                   smallx = lowxpos;
                   smally = lowypos;

                   
                   if(elevation_array[lowxpos][lowypos] <= (sea_level + .005)) //.355)
                   {
                       break;
                   }
                   
                   current_el -= .000001;
                   
                   //if(lowcount == 0)
                   //{
                   //    current_el = next_smallest_el;
                   //}
                    
                   //current_el = .34; //DEBUG
                   //console.log("curel after for loop" + current_el);

                }
            }









    //console.log("While Count: " + count);
    return river_array;


}


function defineRivers3(biome_array, elevation_array, x, y, width, height)
{
    //console.log("Rivers Function");
    var river_array = matrix(width, height, "");
    
    var current_el = 0;

    var wrapx = 0;
    var wrapy = 0;
                
    var smallx = 0;
    var smally = 0;

    var count = 0;

    var lowxpos = 0;
    var lowypos = 0;

    var lowcount = 0;
    
    river_array = biome_array;
    //console.log("got to function");
   
            if(RiverLayer[x][y] > 0)
            {

                //console.log("Identified coordinate as river source");
                current_el = elevation_array[x][y];
                
                wrapx = 0;
                wrapy = 0;
                
                smallx = x;
                smally = y;

                //current_el = 2;  //DEBUG
                //console.log("current_el prewhile loop: " + current_el);

                while(current_el >= sea_level)
                {
                   //console.log("x: " + x + " y: " + y);
                   count++;
                   var smallest_el = current_el;
                   var next_smallest_el = Infinity;
                   lowcount = 0;


                   //console.log("curel inside while loop pre for loop "+current_el);
                    
                   for(var p = -1; p <= 1; p++)   //p = x axis
                   {
                      for(var q = -1; q <= 1; q++)    //q = y axis
                       {
                           
                           if(p == 0 && q == 0)        //Don't test current coordinate
                           {
                               continue;
                           }

                          
                           
                           wrapy = smally + q;

                           if(wrapy >= height || wrapy < 0)     //set up wrapping parameters
                           {
                               continue;
                           }


                           wrapx = smallx + p;

                           if(wrapx >= width)
                           {
                               wrapx = wrapx % width;
                           }
                           if(wrapx < 0)
                           {
                               wrapx = width + p;
                           }

                           
                           //console.log("wx: " + wrapx + " wy: " + wrapy);

                           if(RiverLayer[wrapx][wrapy] > 0)
                           {
                               continue; 
                           }
                           
                           if(elevation_array[wrapx][wrapy] < smallest_el)
                           {
                               smallest_el = elevation_array[wrapx][wrapy];
                               lowxpos = wrapx;
                               lowypos = wrapy;
                               lowcount++;
                               //console.log("lowx: " + lowxpos + " lowy: " + lowypos + " small_el: " + smallest_el);
                           }
                           else if(elevation_array[wrapx][wrapy] < next_smallest_el)
                           {
                               next_smallest_el = elevation_array[wrapx][wrapy];

                           }
                           
                       }
                   }
                   
                   
                   RiverLayer[lowxpos][lowypos] += RiverLayer[x][y]/2;
                   RiverLayer[x][y] /= 2;
                   //river_array[lowxpos][lowypos] = "RIVER";
                   //console.log("slope to next point: " + (elevation_array[smallx][smally] - elevation_array[lowxpos][lowypos]));
                   //current_el = smallest_el;    //DEBUG
                   //console.log("curel after setting to smallel "+current_el);

                   smallx = lowxpos;
                   smally = lowypos;

                   
                   if(elevation_array[lowxpos][lowypos] < (sea_level)) //.355)
                   {
                       break;
                   }
                   
                   current_el -= .000001;
                   
                   //if(lowcount == 0)
                   //{
                   //    current_el = next_smallest_el;
                   //}
                    
                   //current_el = .34; //DEBUG
                   //console.log("curel after for loop" + current_el);

                }
            }









    //console.log("While Count: " + count);
    //return river_array;


}


function generateCities(elevation_array, temperature_array, moisture_array, biome_array, width, height)
{
    var score_array = matrix(width, height, 0);
    var sort_array = [];
    var city_array = [];

    var wrapx = 0;
    var wrapy = 0;
    var percentCities = .00005 * width * height;

    var nearWater = false;

    for(var y = 0; y<height; y++)
    {
        for(var x = 0; x < width; x++)
        {

            nearWater = false;

            for(var p = -1; p <= 1; p++)   //p = x axis
                   {
                      for(var q = -1; q <= 1; q++)    //q = y axis
                       {
                           
                           if(p == 0 && q == 0)        //Don't test current coordinate
                           {
                               continue;
                           }

                          
                           
                           wrapy = y + q;

                           if(wrapy >= height || wrapy < 0)     //set up wrapping parameters
                           {
                               continue;
                           }


                           wrapx = x + p;

                           if(wrapx >= width)
                           {
                               wrapx = wrapx % width;
                           }
                           if(wrapx < 0)
                           {
                               wrapx = width + p;
                           }

                           
                           //console.log("wx: " + wrapx + " wy: " + wrapy);

                           if(biome_array[wrapx][wrapy] == "RIVER" || biome_array[wrapx][wrapy] == "OCEAN" || biome_array[wrapx][wrapy] == "SHALLOW OCEAN")
                           {
                               nearWater = true;
                               break; 
                           }
                           
                                                      
                       }
                   }

                   if (nearWater)
                   {
                       score_array[x][y] = { x: x, y: y, score: moisture_array[x][y] * 1.25 };
                   }
                   else
                   {
                       score_array[x][y] = { x: x, y: y, score: moisture_array[x][y]};
                   }

                   sort_array.push(score_array[x][y]);
                   //if(score_array[x][y].score > 6)
                   //{
                   //    city_array.push(score_array[x][y]);
                   //}


        }
    }
    
    //console.log("pre sort");
    //console.log(score_array);
    sort_array.sort(function(a, b){return b.score-a.score});
    //console.log("post sort");
    //console.log(score_array);
    var xwrapcheck;
    var distance = 0;
    

    for(var s = 0; s < Math.round(percentCities); s++)
    {
        if(s > 0)
        {
            //console.log("hereherehere");
           var dx = Math.abs(sort_array[s].x - city_array[s - 1].x);
                            if(dx > width/2){
                            
                            
                                xwrapcheck = width - dx;
                                 
                                 
                            }
                            else
                            {
                                xwrapcheck = dx;
                                
                            }

                            while (distance < 50)
                            {
                            //for (var i = 0; i < city_array.length; i++)
                            //{
                                distance = Math.sqrt((xwrapcheck * xwrapcheck) + (Math.abs(sort_array[s].y - city_array[s - 1].y) * Math.abs(sort_array[s].y - city_array[s - 1].y)));
                                //sort_array[s].score = sort_array[s].score - (1 / (.5 * sort_array[s].score + 1) * sort_array[s].score);
                                //sort_array.sort(function(a, b){return b.score-a.score});
                                if (distance < 50)
                                {
                                    sort_array.splice(s, 1);
                                }
                            }
                            //}
        }

        
        city_array.push({ x: sort_array[s].x, y: sort_array[s].y, score: sort_array[s].score });
         
        
    }
    
    console.log("city array");
    console.log(city_array);




    return city_array;
}


function generateCities2(elevation_array, temperature_array, moisture_array, biome_array, width, height)
{
    var score_array = matrix(width, height, 0);
    var sort_array = [];
    var city_array = [];

    var wrapx = 0;
    var wrapy = 0;
    var percentCities = .00005 * width * height;

    var nearWater = false;

    for(var y = 0; y<height; y++)
    {
        for(var x = 0; x < width; x++)
        {

            nearWater = false;

            for(var p = -1; p <= 1; p++)   //p = x axis
                   {
                      for(var q = -1; q <= 1; q++)    //q = y axis
                       {
                           
                           if(p == 0 && q == 0)        //Don't test current coordinate
                           {
                               continue;
                           }

                          
                           
                           wrapy = y + q;

                           if(wrapy >= height || wrapy < 0)     //set up wrapping parameters
                           {
                               continue;
                           }


                           wrapx = x + p;

                           if(wrapx >= width)
                           {
                               wrapx = wrapx % width;
                           }
                           if(wrapx < 0)
                           {
                               wrapx = width + p;
                           }

                           
                           //console.log("wx: " + wrapx + " wy: " + wrapy);

                           if(biome_array[wrapx][wrapy] == "RIVER" || biome_array[wrapx][wrapy] == "OCEAN" || biome_array[wrapx][wrapy] == "SHALLOW OCEAN")
                           {
                               nearWater = true;
                               break; 
                           }
                           
                                                      
                       }
                   }

                   if (nearWater)
                   {
                       score_array[x][y] = { x: x, y: y, score: moisture_array[x][y] * 1.25 };
                   }
                   else
                   {
                       score_array[x][y] = { x: x, y: y, score: moisture_array[x][y]};
                   }

                   sort_array.push(score_array[x][y]);
                   //if(score_array[x][y].score > 6)
                   //{
                   //    city_array.push(score_array[x][y]);
                   //}


        }
    }
    
    //console.log("pre sort");
    //console.log(score_array);
    sort_array.sort(function(a, b){return b.score-a.score});
    //console.log("post sort");
    //console.log(score_array);
    var xwrapcheck;
    var distance = 0;
    

    for(var s = 0; s < Math.round(percentCities); s++)
    {
        if (s > 0)
        {
            for (var t = 0; t < city_array.length; t++)
            {
                //if(s > 0)
                //{
                //console.log("hereherehere");
                var dx = Math.abs(sort_array[s].x - city_array[t].x);
                if (dx > width / 2)
                {


                    xwrapcheck = width - dx;


                }
                else
                {
                    xwrapcheck = dx;

                }

                distance = Math.sqrt((xwrapcheck * xwrapcheck) + (Math.abs(sort_array[s].y - city_array[t].y) * Math.abs(sort_array[s].y - city_array[t].y)));

                while (distance < 300)
                {
                //for (var i = 0; i < city_array.length; i++)
                //{
                distance = Math.sqrt((xwrapcheck * xwrapcheck) + (Math.abs(sort_array[s].y - city_array[t].y) * Math.abs(sort_array[s].y - city_array[t].y)));
                //sort_array[s].score = sort_array[s].score - (1 / (.5 * sort_array[s].score + 1) * sort_array[s].score);
                //sort_array.sort(function(a, b){return b.score-a.score});
                console.log("s: " + s + " t: " + t);
                console.log("distance " + distance);
                //console.log("presplice" + sort_array[s].x + " " + sort_array[s].y);
                if (distance < 300)
                {
                    sort_array.splice(s, 1);
                }
                //console.log("postsplice" + sort_array[s-1].x + " " + sort_array[s-1].y);
                //}
                //}
                }
            }
        }
        city_array.push({ x: sort_array[s].x, y: sort_array[s].y, score: sort_array[s].score });
         
        
    }
    
    console.log("city array");
    console.log(city_array);




    return city_array;
}



function generateCities3(elevation_array, temperature_array, moisture_array, biome_array, width, height)
{
    var score_array = matrix(width, height, 0);
    var sort_array = [];
    var city_array = [];

    var wrapx = 0;
    var wrapy = 0;
    var percentCities = Math.round(.00005 * width * height);
    //var percentCities = .0002 * width * height;
    var numCities = Math.floor(Math.pow(Math.random(), .714) * (percentCities - 1)) + 1;

    var nearRiver = false;
    var nearOcean = false;

    for(var y = 0; y < height; y++)
    {
        for(var x = 0; x < width; x++)
        {

            //if (elevation_array[x][y] >= sea_level)
            //{

                nearRiver = false;
                nearOcean = false;

                for (var p = -1; p <= 1; p++)   //p = x axis
                {
                    for (var q = -1; q <= 1; q++)    //q = y axis
                    {

                        if (p == 0 && q == 0)        //Don't test current coordinate
                        {
                            continue;
                        }



                        wrapy = y + q;

                        if (wrapy >= height || wrapy < 0)     //set up wrapping parameters
                        {
                            continue;
                        }


                        wrapx = x + p;

                        if (wrapx >= width)
                        {
                            wrapx = wrapx % width;
                        }
                        if (wrapx < 0)
                        {
                            wrapx = width + p;
                        }


                        //console.log("wx: " + wrapx + " wy: " + wrapy);


                        //------------OLD RIVER CODE----
                        /*if (biome_array[wrapx][wrapy] == "RIVER")// || biome_array[wrapx][wrapy] == "OCEAN" || biome_array[wrapx][wrapy] == "SHALLOW OCEAN")
                        {
                            nearRiver = true;
                            break;
                        }*/
                        //-----------------------------

                        //----New River Code
                        if (RiverLayer[wrapx][wrapy] > 0)// || biome_array[wrapx][wrapy] == "OCEAN" || biome_array[wrapx][wrapy] == "SHALLOW OCEAN")
                        {
                            //console.log("Near River " + wrapx + ", " + wrapy);
                            nearRiver = true;
                            break;
                        }
                        //-------------------


                        if (biome_array[wrapx][wrapy] == "OCEAN" || biome_array[wrapx][wrapy] == "SHALLOW OCEAN")
                        {
                            nearOcean = true;
                            break;
                        }



                    }
                }

                if (nearRiver)
                {
                    score_array[x][y] = { x: x, y: y, score: moisture_array[x][y] + (Math.random()*40 + 10)};//Math.pow(moisture_array[x][y], (1/24)) * 1000 }; //was 50(?)
                }
                //if(nearOcean)
                /*else if (nearOcean)
                {
                    score_array[x][y] = { x: x, y: y, score: moisture_array[x][y]}; //Math.pow(moisture_array[x][y], (1/24)) * 1.1 }; //was 1.1
                }*/
                else// if(!nearOcean && !nearRiver)
                {
                    score_array[x][y] = { x: x, y: y, score: moisture_array[x][y]*Math.random()*.4}; //Math.pow(moisture_array[x][y], (1/24)) };
                }

                sort_array.push(score_array[x][y]);
                //if(score_array[x][y].score > 6)
                //{
                //    city_array.push(score_array[x][y]);
                //}

            //}
                //drawArray(x, y, score_array[x][y].score*.5, "myCanvas12");
        }
    }
    
    //console.log("pre sort");
    //console.log(score_array);
    sort_array.sort(function(a, b){return b.score-a.score});
    //console.log("post sort");
    //console.log(score_array);

    //console.log("SArray");
    //console.log(sort_array);
    var xwrapcheck;
    var distance = 0;
    var i = -1;
    var j = -1;
    var consecutive_j = false;
    var count = 0;
    var popsize = "";

    var popval = Math.random();
    if(popval < .35){
        popsize = "small";
    }
    else if(popval < .85){
        popsize = "medium";
    }
    else{
        popsize = "large";
    }

    //var b = 0;
    while (city_array.length < 1)
    {
        if (elevation_array[sort_array[0].x][sort_array[0].y] >= sea_level)
        {
            city_array.push({ x: sort_array[0].x, y: sort_array[0].y, score: sort_array[0].score, population: popsize });  // was [0] instead of [b]
        }
        else
        {
            sort_array.splice(0, 1);
            
            //console.log("SASplice");
            //console.log(sort_array[0])   
        }
        if(sort_array.length == 0)
        {
            break;    
        }
        //b++;
   }
    //for (var j = 0; j < Math.round(percentCities); j++)
    //{

   if (sort_array.length != 0)
   {
       for (var s = 0; s < Infinity; s++)
       {

           j++;
           while (distance < 35)
           {
               count++;
               if (count > 1)
               {
                   j = 0;
               }
               i++;
               //console.log(i);
               var dx = Math.abs(sort_array[i].x - city_array[j].x);
               if (dx > width / 2)
               {


                   xwrapcheck = width - dx;


               }
               else
               {
                   xwrapcheck = dx;

               }
               distance = Math.sqrt((xwrapcheck * xwrapcheck) + (Math.abs(sort_array[i].y - city_array[j].y) * Math.abs(sort_array[i].y - city_array[j].y)));
               ///console.log("s, i, j " + s + " " + i + " " + j);
               //console.log("x, y " + sort_array[i].x + " " + sort_array[i].y);
               //console.log("dist: " + distance);


               //IF more than 1 execution of while loop within the same s variable, j returns to 0
               //if(distance > 100)
               //{
               //    jtest++;
               //}
               //else
               //{
               //    jtest = 0;
               //}


           }
           if (j == city_array.length - 1)
           {
               //console.log("Pushed");
               if (elevation_array[sort_array[i].x][sort_array[i].y] >= sea_level)
               {
                   var popval = Math.random();
                   if (popval < .35)
                   {
                       popsize = "small";
                   }
                   else if (popval < .85)
                   {
                       popsize = "medium";
                   }
                   else
                   {
                       popsize = "large";
                   }

                   //if (elevation_array[sort_array[i].x][sort_array[i].y] >= sea_level)
                   //{
                   city_array.push({ x: sort_array[i].x, y: sort_array[i].y, score: sort_array[i].score, population: popsize });
               }
               //}
               j = -1;
               //i = -1;
               //i--;
               //distance = 0;

           }
           else
           {
               //if(jtest != (j + 1))      NEED WAY TO TELL WHEN ITERATIONS WITH DISTANCE > 100 ARE CONSECUTIVE, BUT THIS CODE HANGS
               //{
               //    j = -1;
               //}
               i--;
           }
           distance = 0;
           count = 0;
           //else
           //{
           //  j++;   
           //}

           //if(city_array.length == Math.round(percentCities))
           if (city_array.length == numCities || i == sort_array.length - 1)
           {
               break;
           }
       }
   }
   
    /*for(var s = 0; s < city_array.length; s++)
    {
        if (s > 0)
        {
            for (var t = 0; t < sort_array.length; t++)
            {
                //if(s > 0)
                //{
                //console.log("hereherehere");
                var dx = Math.abs(sort_array[t].x - city_array[s].x);
                if (dx > width / 2)
                {


                    xwrapcheck = width - dx;


                }
                else
                {
                    xwrapcheck = dx;

                }

                distance = Math.sqrt((xwrapcheck * xwrapcheck) + (Math.abs(sort_array[s].y - city_array[t].y) * Math.abs(sort_array[s].y - city_array[t].y)));

                while (distance < 300)
                {
                //for (var i = 0; i < city_array.length; i++)
                //{
                distance = Math.sqrt((xwrapcheck * xwrapcheck) + (Math.abs(sort_array[s].y - city_array[t].y) * Math.abs(sort_array[s].y - city_array[t].y)));
                //sort_array[s].score = sort_array[s].score - (1 / (.5 * sort_array[s].score + 1) * sort_array[s].score);
                //sort_array.sort(function(a, b){return b.score-a.score});
                console.log("s: " + s + " t: " + t);
                console.log("distance " + distance);
                //console.log("presplice" + sort_array[s].x + " " + sort_array[s].y);
                if (distance < 300)
                {
                    sort_array.splice(s, 1);
                }
                //console.log("postsplice" + sort_array[s-1].x + " " + sort_array[s-1].y);
                //}
                //}
                }
            }
        }
        city_array.push({ x: sort_array[s].x, y: sort_array[s].y, score: sort_array[s].score });
         
        
    }*/
    
    console.log("city array");
    console.log(city_array);




    return city_array;
}

function mutateLanguage(origLang)
{
    var numMutations = Math.floor(Math.random() * 6) + 1;
    
    
    var mutationList = ["addSound", "removeSound", "changeSylMin", "changeSylMax", "changeCharMin", "changeCharMax", "soundShift", "changeRestricts", "changeStructure"];
    var allSounds = "aeiouAEIOUptkbdgmnhjqvwlrsxfzʃʒʧʤŋɣʔ"
    var allV = "aeiouAEIOU";
    var allC = "bdfghjklmnpqrstvwxzʃʒʧʤŋɣ";
    var allL = "rlwj";
    var allS = "sʃf";
    var allF = "sʃzʒmnŋk";

    //var newLang = [];
    //var newLang = origLang;
    //var newLang = origLang.clone();


    //for(var i=0;i<origLang.length;i++){
    // newLang.push(origLang[i]);
    //}

    //var newLang = deepObjCopy(origLang);


    //-------------------------------------------
    var newLang = makeBasicLanguage();
    newLang.noortho = false;
    newLang.nomorph = false;
    newLang.nowordpool = false;

    newLang.definite = origLang.definite;
    newLang.genitive = origLang.genitive;

    newLang.phonemes.C = [];
    newLang.phonemes.V = [];
    newLang.phonemes.L = [];
    newLang.phonemes.S = [];
    newLang.phonemes.F = [];
    newLang.restricts = [];

    for (var c = 0; c < origLang.phonemes.C.length; c++)
    {
        newLang.phonemes.C[c] = origLang.phonemes.C[c];
    }
     for (var c = 0; c < origLang.phonemes.V.length; c++)
    {
        newLang.phonemes.V[c] = origLang.phonemes.V[c];
    }
     for (var c = 0; c < origLang.phonemes.L.length; c++)
    {
        newLang.phonemes.L[c] = origLang.phonemes.L[c];
    }
     for (var c = 0; c < origLang.phonemes.S.length; c++)
    {
        newLang.phonemes.S[c] = origLang.phonemes.S[c];
    }
     for (var c = 0; c < origLang.phonemes.F.length; c++)
    {
        newLang.phonemes.F[c] = origLang.phonemes.F[c];
    }
    //newLang.phonemes.V = shuffled(choose(vowsets, 2).V);
    //newLang.phonemes.L = shuffled(choose(lsets, 2).L);
    //newLang.phonemes.S = shuffled(choose(ssets, 2).S);
    //newLang.phonemes.F = shuffled(choose(fsets, 2).F);
    newLang.structure = origLang.structure;
    for (var c = 0; c < origLang.restricts.length; c++)
    {
        newLang.restricts[c] = origLang.restricts[c];
    }
    //newLang.restricts = ressets[2].res;
    newLang.cortho = origLang.cortho;
    newLang.vortho = origLang.vortho;
    newLang.minsyll = origLang.minsyll;
    newLang.maxsyll = origLang.maxsyll;
    newLang.joiner = origLang.joiner;
    //--------------------------------------------------------------
    //console.log("NEWLANG");
    //console.log(newLang);

    //console.log("STR CHECK");
    //console.log(newLang.phonemes.C);

    for (var i = 0; i < numMutations; i++ ){
        var mutationType = choose(mutationList);
        //var mutationType = "changeStructure";
        //console.log(mutationType);
        switch (mutationType)
        {
            case "addSound":

                var isNew = false;
                while (!isNew)
                {
                    var potentialSound = allSounds[Math.floor(Math.random() * allSounds.length)];
                    if (origLang.phonemes.C.indexOf(potentialSound) == -1
                     && origLang.phonemes.V.indexOf(potentialSound) == -1
                     && origLang.phonemes.L.indexOf(potentialSound) == -1
                     && origLang.phonemes.S.indexOf(potentialSound) == -1
                     && origLang.phonemes.F.indexOf(potentialSound) == -1
                     )
                    {
                        if (allV.includes(potentialSound))
                        {
                            //newLang.phonemes.V.concat(potentialSound);
                            newLang.phonemes.V.push(potentialSound);
                        }
                        if (allC.includes(potentialSound))
                        {
                            //newLang.phonemes.C.concat(potentialSound);
                            newLang.phonemes.C.push(potentialSound);
                        }
                        if (allL.includes(potentialSound))
                        {
                            //newLang.phonemes.L.concat(potentialSound);
                            newLang.phonemes.L.push(potentialSound);
                        }
                        if (allS.includes(potentialSound))
                        {
                            //newLang.phonemes.S.concat(potentialSound);
                            newLang.phonemes.S.push(potentialSound);
                        }
                        if (allF.includes(potentialSound))
                        {
                            //newLang.phonemes.F.concat(potentialSound);
                            newLang.phonemes.F.push(potentialSound);
                        }
                        isNew = true;
                    }

                }
                break;

            case "removeSound":

                var soundType = Math.floor(Math.random() * 5);
                switch (soundType)
                {
                    case 0:
                        //newLang.phonemes.V.replace(newLang.phonemes.V[Math.floor(Math.random() * newLang.phonemes.V.length)], '');
                        if (newLang.phonemes.V.length == 1)
                        {
                            newLang.phonemes.V.splice(Math.floor(Math.random() * newLang.phonemes.V.length), 1, "");
                        }
                        else
                        {
                            newLang.phonemes.V.splice(Math.floor(Math.random() * newLang.phonemes.V.length), 1);
                        }
                        break;

                    case 1:
                        //newLang.phonemes.C.replace(newLang.phonemes.C[Math.floor(Math.random() * newLang.phonemes.C.length)], '');
                        if (newLang.phonemes.C.length == 1)
                        {
                            newLang.phonemes.C.splice(Math.floor(Math.random() * newLang.phonemes.C.length), 1, "");
                        }
                        else
                        {
                            newLang.phonemes.C.splice(Math.floor(Math.random() * newLang.phonemes.C.length), 1);
                        }
                        break;

                    case 2:
                        //newLang.phonemes.L.replace(newLang.phonemes.L[Math.floor(Math.random() * newLang.phonemes.L.length)], '');
                        if (newLang.phonemes.L.length == 1)
                        {
                            newLang.phonemes.L.splice(Math.floor(Math.random() * newLang.phonemes.L.length), 1, "");
                        }
                        else
                        {
                            newLang.phonemes.L.splice(Math.floor(Math.random() * newLang.phonemes.L.length), 1);
                        }
                        break;

                    case 3:
                        //newLang.phonemes.S.replace(newLang.phonemes.S[Math.floor(Math.random() * newLang.phonemes.S.length)], '');
                        if (newLang.phonemes.S.length == 1)
                        {
                            newLang.phonemes.S.splice(Math.floor(Math.random() * newLang.phonemes.S.length), 1, "");
                        }
                        else
                        {
                            newLang.phonemes.S.splice(Math.floor(Math.random() * newLang.phonemes.S.length), 1);
                        }
                        break;

                    case 4:
                        //newLang.phonemes.F.replace(newLang.phonemes.F[Math.floor(Math.random() * newLang.phonemes.F.length)], '');
                        if (newLang.phonemes.F.length == 1)
                        {
                            newLang.phonemes.F.splice(Math.floor(Math.random() * newLang.phonemes.F.length), 1, "");
                        }
                        else
                        {
                            newLang.phonemes.F.splice(Math.floor(Math.random() * newLang.phonemes.F.length), 1);
                        }
                        break;

                    default:
                        break;
                }
                break;

            case "changeSylMin":

                if (Math.random() < .5)
                {
                    if (newLang.minsyll > 1)
                    {
                        newLang.minsyll--;
                    }
                }
                else
                {
                    newLang.minsyll++;
                }
                break;

            case "changeSylMax":
                var syllChange = Math.floor(Math.random() * 5) - 2;
                if (newLang.maxsyll + syllChange <= newLang.minsyll)
                {
                    newLang.maxsyll = newLang.minsyll + 1;
                }
                else
                {
                    newLang.maxsyll = newLang.maxsyll + syllChange;
                }

                break;

            case "changeCharMin":
                var charChange = Math.floor(Math.random() * 5) - 2;

                newLang.minchar = newLang.minchar + charChange;

                break;

            case "changeCharMax":

                var charChange = Math.floor(Math.random() * 11) - 5;
                if (newLang.maxchar + charChange <= newLang.minchar)
                {
                    newLang.maxchar = newLang.minchar + 1;
                }
                else
                {
                    newLang.maxchar = newLang.maxchar + charChange;
                }
                break;

            case "soundShift":
                //possibly update with sound changes that are "likely" - add probability system
                var shiftGroup = choose(["C", "V", "L", "S", "F"]);
                //var potentialSound = allSounds[Math.floor(Math.random() * allSounds.length)];
                if (shiftGroup == "C")
                {
                    //newLang.phonemes.C.replace(newLang.phonemes.C[Math.floor(Math.random() * newLang.phonemes.C.length)], allC[Math.floor(Math.random() * allC.length)]);
                    newLang.phonemes.C.splice(Math.floor(Math.random() * newLang.phonemes.C.length), 1, allC[Math.floor(Math.random() * allC.length)]);
                }
                if (shiftGroup == "V")
                {
                    //newLang.phonemes.V.replace(newLang.phonemes.V[Math.floor(Math.random() * newLang.phonemes.V.length)], allV[Math.floor(Math.random() * allV.length)]);
                    newLang.phonemes.V.splice(Math.floor(Math.random() * newLang.phonemes.V.length), 1, allV[Math.floor(Math.random() * allV.length)]);
                }
                if (shiftGroup == "L")
                {
                    //newLang.phonemes.L.replace(newLang.phonemes.L[Math.floor(Math.random() * newLang.phonemes.L.length)], allL[Math.floor(Math.random() * allL.length)]);
                    newLang.phonemes.L.splice(Math.floor(Math.random() * newLang.phonemes.L.length), 1, allL[Math.floor(Math.random() * allL.length)]);
                }
                if (shiftGroup == "S")
                {
                    //newLang.phonemes.S.replace(newLang.phonemes.S[Math.floor(Math.random() * newLang.phonemes.S.length)], allS[Math.floor(Math.random() * allS.length)]);
                    newLang.phonemes.S.splice(Math.floor(Math.random() * newLang.phonemes.S.length), 1, allS[Math.floor(Math.random() * allS.length)]);
                }
                if (shiftGroup == "F")
                {
                    //newLang.phonemes.F.replace(newLang.phonemes.F[Math.floor(Math.random() * newLang.phonemes.F.length)], allF[Math.floor(Math.random() * allF.length)]);
                    newLang.phonemes.F.splice(Math.floor(Math.random() * newLang.phonemes.F.length), 1, allF[Math.floor(Math.random() * allF.length)]);
                }

                break;

            case "changeRestricts":
                newLang.restricts = ressets[Math.floor(Math.random() * 3)];
                break;

            case "changeStructure":
                newLang.structure = choose(syllstructs);
                break;

            default:
                break;


        }
    }
    //console.log("New/Orig Lang within Mutate");
    //console.log(newLang);
    //console.log(origLang);
    return newLang;  
}

function generateLanguages()
{
    /*var numLangs = Math.floor(Math.random() * CityArray.length) + 1;  //Number of languages from 1 to Number of cities
    for (var d = 0; d < numLangs; d++)
    {
    LangArray.push(makeRandomLanguage());
    }
    //randLang = makeRandomLanguage();
    for (var c = 0; c < CityArray.length; c++)
    {
    var chooseLang = Math.floor(Math.random() * (LangArray.length));
    //CityArray[c].name = makeName(randLang);
    CityArray[c].name = makeName(LangArray[chooseLang]);
    }
    //console.log(randLang);
    console.log(LangArray);*/

    var usedCityIndices = [];
    var langStartCity = 0;
    //var mutationList = ["addSound", "removeSound", "changeSylMin", "changeSylMax", "changeCharMin", "changeCharMax", "soundShift", "changeRestricts", "changeStructure"];
    var numLangs = Math.floor(Math.random() * CityArray.length) + 1;  //Number of languages from 1 to Number of cities
    for (var d = 0; d < numLangs; d++)
    {
        LangArray.push(makeRandomLanguage());

        langStartCity = Math.floor(Math.random() * (CityArray.length));
        while (usedCityIndices.indexOf(langStartCity) > -1)
        {
            langStartCity = Math.floor(Math.random() * (CityArray.length));
        }
        //if (usedCityIndices.indexOf(langStartCity) == -1)
        //{
        CityArray[langStartCity].name = makeName(LangArray[d]);
        CityArray[langStartCity].lang = LangArray[d];
        CityArray[langStartCity].langTitle = "Lang " + d;
        CityArray[langStartCity].family = d;
        LangArray[d].title = "Lang " + d;
        LangArray[d].family = d; 
        LangArray[d].name = capitalize(makeWord(LangArray[d], "language"));
        usedCityIndices.push(langStartCity);

        
        //}
        //CityArray[langStartCity].name = makeName(LangArray[d]);
    }
    //randLang = makeRandomLanguage();
    var cityDistances = [];
    for (var c = 0; c < CityArray.length; c++)
    {
        cityDistances = [];
        if (usedCityIndices.indexOf(c) > -1) { continue; }

        var xwrapcheck = 0;
        for (var u = 0; u < usedCityIndices.length; u++)
        {
            var dx = Math.abs(CityArray[usedCityIndices[u]].x - CityArray[c].x);
            if (dx > width / 2)
            {
                xwrapcheck = width - dx;
            }
            else
            {
                xwrapcheck = dx;
            }
            var cityDistance = Math.sqrt((xwrapcheck * xwrapcheck) + ((CityArray[usedCityIndices[u]].y - CityArray[c].y) * (CityArray[usedCityIndices[u]].y - CityArray[c].y)));
            //console.log("cD: "+cityDistance);
            cityDistances.push({ homeIndex: c, awayIndex: usedCityIndices[u], distance: cityDistance });
        }
        cityDistances.sort(function (a, b) { return b.distance - a.distance });
        //console.log("cityDist");
        //console.log(cityDistances);
        //var mutationType = "";
        var resultingLang = [];

        for (var s = 0; s < cityDistances.length; s++)
        {
            var prob_const = 40 / (CityArray.length * 1.25) / 10;
            if (s == (cityDistances.length - 1))
            {
                if (Math.random() < .4) //non mutated language
                {
                    CityArray[cityDistances[s].homeIndex].name = makeName(CityArray[cityDistances[s].awayIndex].lang);
                    CityArray[cityDistances[s].homeIndex].lang = CityArray[cityDistances[s].awayIndex].lang;
                    CityArray[cityDistances[s].homeIndex].langTitle = CityArray[cityDistances[s].awayIndex].langTitle;
                    CityArray[cityDistances[s].homeIndex].family = CityArray[cityDistances[s].awayIndex].family;
                    CityArray[cityDistances[s].homeIndex].isMutation = false;

                }
                else
                {
                    var familyMutations = [];
                        for (var r = 0; r < LangArray.length; r++ )
                        {
                            if(LangArray[r].family == CityArray[cityDistances[s].awayIndex].family && LangArray[r].title.includes("Descendant"))
                            {
                                familyMutations.push(LangArray[r]);   
                            }
                        }

                    if (Math.random() < .3 && familyMutations.length > 0)  //city with mutated language that already exists
                    {
                        
                        
                        console.log("FM");
                        console.log(familyMutations);
                        resultingLang = familyMutations[Math.floor(Math.random() * (familyMutations.length))];
                        //mutationType = choose(mutationList);
                        //resultingLang = mutateLanguage(CityArray[cityDistances[s].awayIndex].lang);
                        //LangArray.push(resultingLang);
                        //CityArray[cityDistances[s].homeIndex].name = makeName(CityArray[cityDistances[s].awayIndex].lang);
                        //CityArray[cityDistances[s].homeIndex].lang = CityArray[cityDistances[s].awayIndex].lang;
                        CityArray[cityDistances[s].homeIndex].name = makeName(resultingLang);
                        CityArray[cityDistances[s].homeIndex].lang = resultingLang;
                        CityArray[cityDistances[s].homeIndex].langTitle = resultingLang.title;
                        CityArray[cityDistances[s].homeIndex].family = resultingLang.family;
                        CityArray[cityDistances[s].homeIndex].isMutation = true;
                        //LangArray[LangArray.length - 1].title = CityArray[cityDistances[s].awayIndex].langTitle.concat(" Descendant");
                        //LangArray[LangArray.length - 1].family = CityArray[cityDistances[s].awayIndex].family;
                        //LangArray[LangArray.length - 1].name = capitalize(makeWord(resultingLang, "language"));
                    }
                    else    //city with new mutated language
                    {
                        //mutationType = choose(mutationList);
                        resultingLang = mutateLanguage(CityArray[cityDistances[s].awayIndex].lang);
                        LangArray.push(resultingLang);
                        //CityArray[cityDistances[s].homeIndex].name = makeName(CityArray[cityDistances[s].awayIndex].lang);
                        //CityArray[cityDistances[s].homeIndex].lang = CityArray[cityDistances[s].awayIndex].lang;
                        CityArray[cityDistances[s].homeIndex].name = makeName(resultingLang);
                        CityArray[cityDistances[s].homeIndex].lang = resultingLang;
                        CityArray[cityDistances[s].homeIndex].langTitle = CityArray[cityDistances[s].awayIndex].langTitle.concat(" Descendant");
                        CityArray[cityDistances[s].homeIndex].family = CityArray[cityDistances[s].awayIndex].family;
                        CityArray[cityDistances[s].homeIndex].isMutation = true;
                        LangArray[LangArray.length - 1].title = CityArray[cityDistances[s].awayIndex].langTitle.concat(" Descendant");
                        LangArray[LangArray.length - 1].family = CityArray[cityDistances[s].awayIndex].family;
                        LangArray[LangArray.length - 1].name = capitalize(makeWord(resultingLang, "language"));
                    }
                }
                break;
            }
            if (Math.random() * 100 < (prob_const * (s + 1) * (s + 1)))
            {
                if (Math.random() < .4)
                {
                    CityArray[cityDistances[s].homeIndex].name = makeName(CityArray[cityDistances[s].awayIndex].lang);
                    CityArray[cityDistances[s].homeIndex].lang = CityArray[cityDistances[s].awayIndex].lang;
                    CityArray[cityDistances[s].homeIndex].langTitle = CityArray[cityDistances[s].awayIndex].langTitle;
                    CityArray[cityDistances[s].homeIndex].family = CityArray[cityDistances[s].awayIndex].family;
                    CityArray[cityDistances[s].homeIndex].isMutation = false;
                }
                else
                {

                    var familyMutations = [];
                        for (var r = 0; r < LangArray.length; r++ )
                        {
                            if(LangArray[r].family == CityArray[cityDistances[s].awayIndex].family && LangArray[r].title.includes("Descendant"))
                            {
                                familyMutations.push(LangArray[r]);   
                            }
                        }

                    if (Math.random() < .3 && familyMutations.length > 0)  //city with mutated language that already exists
                    {
                        
                        console.log("FM");
                        console.log(familyMutations);
                        resultingLang = familyMutations[Math.floor(Math.random() * (familyMutations.length))];
                        //mutationType = choose(mutationList);
                        //resultingLang = mutateLanguage(CityArray[cityDistances[s].awayIndex].lang);
                        //LangArray.push(resultingLang);
                        //CityArray[cityDistances[s].homeIndex].name = makeName(CityArray[cityDistances[s].awayIndex].lang);
                        //CityArray[cityDistances[s].homeIndex].lang = CityArray[cityDistances[s].awayIndex].lang;
                        CityArray[cityDistances[s].homeIndex].name = makeName(resultingLang);
                        CityArray[cityDistances[s].homeIndex].lang = resultingLang;
                        CityArray[cityDistances[s].homeIndex].langTitle = resultingLang.title;
                        CityArray[cityDistances[s].homeIndex].family = resultingLang.family;
                        CityArray[cityDistances[s].homeIndex].isMutation = true;
                        //LangArray[LangArray.length - 1].title = CityArray[cityDistances[s].awayIndex].langTitle.concat(" Descendant");
                        //LangArray[LangArray.length - 1].family = CityArray[cityDistances[s].awayIndex].family;
                        //LangArray[LangArray.length - 1].name = capitalize(makeWord(resultingLang, "language"));
                    }
                    else
                    {
                        resultingLang = mutateLanguage(CityArray[cityDistances[s].awayIndex].lang);
                        LangArray.push(resultingLang);
                        //console.log("ResLang");
                        //console.log(resultingLang);
                        //console.log("OrigLang");
                        //console.log(CityArray[cityDistances[s].awayIndex].lang);
                        //CityArray[cityDistances[s].homeIndex].name = makeName(CityArray[cityDistances[s].awayIndex].lang);
                        //CityArray[cityDistances[s].homeIndex].lang = CityArray[cityDistances[s].awayIndex].lang;
                        CityArray[cityDistances[s].homeIndex].name = makeName(resultingLang);
                        CityArray[cityDistances[s].homeIndex].lang = resultingLang;
                        CityArray[cityDistances[s].homeIndex].langTitle = CityArray[cityDistances[s].awayIndex].langTitle.concat(" Descendant");
                        CityArray[cityDistances[s].homeIndex].family = CityArray[cityDistances[s].awayIndex].family;
                        CityArray[cityDistances[s].homeIndex].isMutation = true;
                        LangArray[LangArray.length - 1].title = CityArray[cityDistances[s].awayIndex].langTitle.concat(" Descendant");
                        LangArray[LangArray.length - 1].family = CityArray[cityDistances[s].awayIndex].family;
                        LangArray[LangArray.length - 1].name = capitalize(makeWord(resultingLang, "language"));
                    }
                }
                break;
            }
        }
        //
        //var chooseLang = Math.floor(Math.random() * (LangArray.length));
        //CityArray[c].name = makeName(LangArray[chooseLang]);
        //



        //CityArray[c].name = makeName(randLang);
        //CityArray[c].name = makeName(LangArray[chooseLang]);
    }


    var famColors = ["red", "yellow", "limegreen", "cyan", "orange", "#FF3BBF", "white", "blueviolet", "seagreen", "#3E37DF", "#E23F16", "#1289C9"];

    for (var i = 0; i < LangArray.length; i++)
    {
        if (i < famColors.length)
        {
            LangFamilyColors[i] = famColors[i];
        }
        else
        {
            LangFamilyColors[i] = get_random_color();
        }

    }

    //console.log(randLang);
    console.log("LA");
    console.log(LangArray);
    console.log("CA");
    console.log(CityArray);
}


function stepErosion(isRiverMode)
{

    //console.log("in erosion function");
    var x1, y1;
    
    for(var x = 0; x < width; x++)
    {
        for(var y = 0; y < height; y++)
        {

            x1 = x;
            y1 = y;

            if (!isRiverMode)
            {
                if (WaterLayer[x1][y1] <= 0)
                {
                    continue;
                }
            }
            else
            {
                if (RiverLayer[x1][y1] <= 0)
                {
                    continue;
                }
            }


            //if (x1 - 1 < 0)
            //{
           //     var leftx = width - 1; //Math.max(0, x1 - 1);
           // }
           // else
            //{
            //    var leftx = x1 - 1;
           // }
            var leftx = x1 - 1;
            var bottomy = Math.max(0, y1 - 1);

            //if (x1 + 1 >= width)
           // {
           //     var rightx = (x1 + 1) % width;//Math.min(width - 1, x1 + 1);
           // }
           // else
          // {
           //     var rightx = x1 + 1;
           // }
            var rightx = x1 + 1;
            var maxy = Math.min(height - 1, y1 + 1);

            var minEl = Infinity;
            var minx = 0;
            var miny = 0;

            for(var i = leftx; i <= rightx; i++)
            {
                for(var j = bottomy; j <= maxy; j++)
                {
                    var wrapi = i;
                    if(i < 0)
                    {
                        wrapi = width - 1;
                    }
                    if(i >= width)
                    {
                        wrapi = i % width;
                    }
                    

                    if(wrapi != x1 || j != y1)
                    {
                        if (!isRiverMode)
                        {
                            var elev = ModifiedElevationArray[wrapi][j] + WaterLayer[wrapi][j];
                        }
                        else
                        {
                            var elev = ModifiedElevationArray[wrapi][j] + RiverLayer[wrapi][j];
                        }
                        if(elev < minEl)
                        {
                            minEl = elev;
                            minx = wrapi;
                            miny = j;
                        }   
                    

                    }
                }
            }


            if (!isRiverMode)
            {
                var dif = .5 * (ModifiedElevationArray[x1][y1] + WaterLayer[x1][y1] - minEl);
            }
            else
            {
                var dif = .5 * (ModifiedElevationArray[x1][y1] + RiverLayer[x1][y1] - minEl);
            }
            //console.log("first dif " + dif);


            if(dif > 0)
            {

                var erosion = dif; //*(1 - soilhardness)
                //console.log("erosion " + erosion);
                if (!isRiverMode)
                {
                    ModifiedElevationArray[x1][y1] -= erosion;

                    //ModifiedElevationArray[minx][miny] += erosion * .9;


                    dif = .5 * (ModifiedElevationArray[x1][y1] + WaterLayer[x1][y1] - (ModifiedElevationArray[minx][miny] + WaterLayer[minx][miny]));

                    WaterLayer[x1][y1] -= dif;
                    WaterLayer[minx][miny] += dif;
                }
                else
                {
                    
                    dif = .5 * (ModifiedElevationArray[x1][y1] + RiverLayer[x1][y1] - (ModifiedElevationArray[minx][miny] + RiverLayer[minx][miny]));

                    RiverLayer[x1][y1] -= dif;
                    RiverLayer[minx][miny] += dif;
                }
                //console.log("water dif " + dif);
                

            }


        }
    }  





}


function drawSatelliteView(x, xt, y, yt, MoistureArrayVal, ElevationArrayVal, AvgMoistureArrayVal, canvas_id)
        {

            var c = document.getElementById(canvas_id);
            var ctx = c.getContext("2d");

            
            var moistureOpacity;
            
            


            //for (var y = 0; y < height; y++)
            //{
               // for (var x = 0; x < width; x++)
                //{

                        if (BiomeMap[x][y] == "ROCKY MOUNTAIN")
                        {
                            var elFactor = Math.sqrt((ElevationArrayVal - .68) * 8.33)*ElevationArrayVal;
                            ctx.fillStyle = "rgba(137, 43, 12," + elFactor + ")";
                            ctx.fillRect(xt, yt, 1, 1);
                        }
                    
                        moistureOpacity = AvgMoistureArrayVal;

                        //if (moistureOpacity > 1) { moistureOpacity = 1; }
                        //moistureOpacity = .7*Math.pow(moistureOpacity, (1/2));
                        //moistureOpacity = (moistureOpacity - .1) / (moistureOpacity * 1.1 + 1);
                        //moistureOpacity = moistureOpacity / 10;
                        moistureOpacity = (moistureOpacity) / (moistureOpacity * 1.1 + 1);
                        //moistureOpacity = .5*Math.pow(moistureOpacity, (1/3));
                        //console.log(moistureOpacity);

                        /*if (BiomeArray[x][y - halfHeightDiff] == "TROPICAL RAIN FOREST" || BiomeArray[x][y - halfHeightDiff] == "TROPICAL SEASONAL FOREST"
                        || BiomeArray[x][y - halfHeightDiff] == "TEMPERATE RAIN FOREST" || BiomeArray[x][y - halfHeightDiff] == "TEMPERATE SEASONAL FOREST")
                        {
                        //moistureOpacity *= 5;
                        //ctx.fillStyle = "rgba(8, 45, 4," + moistureOpacity + ")";
                        }
                        else if (BiomeArray[x][y - halfHeightDiff] == "SHRUBLAND" || BiomeArray[x][y - halfHeightDiff] == "SAVANNAH"
                        || BiomeArray[x][y - halfHeightDiff] == "CHAPPARAL" || BiomeArray[x][y - halfHeightDiff] == "GRASSLAND" || BiomeArray[x][y - halfHeightDiff] == "COASTLAND")
                        {
                        //moistureOpacity += moistureOpacity * .2;
                        //ctx.fillStyle = "rgba(8, 55, 3," + moistureOpacity + ")";
                        }*/

                        var colorFactor = moistureOpacity * 2 * AvgMoistureArrayVal * AvgMoistureArrayVal * .07;
                        if (colorFactor > 1) { colorFactor = 1; }

                        ctx.fillStyle = "rgba(8, 63, 3," + moistureOpacity + ")";
                        ctx.fillStyle = "rgba(" + Math.floor((1 - colorFactor) * 8) + ", " + Math.floor(colorFactor * 48 + (1 - colorFactor) * 63) + ", " + Math.floor(colorFactor * 12 + (1 - colorFactor) * 3) + "," + moistureOpacity + ")";

                        if (ElevationArrayVal >= sea_level)
                        {
                            ctx.fillRect(xt, yt, 1, 1);
                        }
                        if (ElevationArrayVal > sea_level && ElevationArrayVal <= sea_level + .01)
                        {
                            ctx.fillStyle = "rgba(222, 232, 187, .2";
                            ctx.fillRect(xt, yt, 1, 1);
                        }
                        if (BaseTemperatureMap[x][y] < .2)
                        {
                            var tempcheck = BaseTemperatureMap[x][y];
                            if (tempcheck < 0) { tempcheck = 0; }
                            ctx.fillStyle = "rgba(232, 246, 255," + (1 - (25 * Math.pow(tempcheck, 2)))/*(1 - (5*TemperatureArray[x][y - halfHeightDiff]))*/ + ")";
                        }





                        //ctx.fillStyle = "rgba(8, 63, 3," + moistureOpacity + ")";


                        if (ElevationArrayVal < sea_level)
                        {
                            ctx.fillStyle = "#0a2044";
                            if (ElevationArrayVal >= .5714*sea_level) //.2)
                            {
                                ctx.fillStyle = "rgba(13, 63, 130," + ((ElevationArrayVal) / (sea_level)) + ")";
                            }
                            //else if (ElevationArray[x][y - halfHeightDiff] < .02)
                            //{
                            //    ctx.fillStyle = "rgba(10, 32, 68," + ((Math.pow(ElevationArray[x][y - halfHeightDiff],2))/(.02*.02)) + ")";
                            //}
                        }
                        if (BiomeMap[x][y] == "ICE")
                        {
                            //ctx.fillStyle = "#e8f6ff";
                            ctx.fillStyle = "#ddf2ff";
                        }

                        
                        if (RiverLayer[x][y] > 0)
                        {
                            ctx.fillStyle = "#0a2044";
                            
                        }

                    

                    ctx.fillRect(xt, yt, 1, 1);




               // }
            //}



        }


function generateNormalMap()
{
    //test with 1481777886249


    //function normalmap(canvasId, texture, normalmap, specularity, shiny) {
    var canvas = document.getElementById("myCanvas11");
    var ctx = canvas.getContext('2d');
    var bcanvas = document.getElementById("bCanvas");
    var bctx = bcanvas.getContext('2d');

    bctx.clearRect(0, 0, width, height);

    var spec = document.getElementById("Specularity");
    var shine = document.getElementById("Shiny");
    var lightx = document.getElementById("lightx");
    var lighty = document.getElementById("lighty"); 
    var lightz = document.getElementById("lightz");  


    var normalData = null;
    var textureData = null;
    var shiny = 0;
    var specularity = 1.2;

    /*function getDataFromImage(img) {
        canvas.width = img.width;
        canvas.height = img.height;
        ctx.clearRect(0, 0, img.width, img.height);
        ctx.drawImage(img, 0 ,0);
        return ctx.getImageData(0, 0, img.width, img.height);
    }

    function loadImage(src, callback) {
        var img = document.createElement('img');
        img.onload = callback;
        img.src = src;
        return img;
    }*/
    
    
    bctx.scale(scale, scale);

    createBumpMap();

    var prescaled_img = bctx.getImageData(0, 0, width, height);
    inMemCtx.putImageData(prescaled_img, 0, 0);

    bctx.scale(1/scale, 1/scale);

    bctx.drawImage(MemCanvas, 0, 0);

    var normals = [];
    var textureData = null;


    /*if (normalmaptexture.complete)
                        {
                            bctx.drawImage(normalmaptexture, 0, 0);
                        } else
                        {
                            normalmaptexture.onload = function ()
                            {
                                bctx.drawImage(normalmaptexture, 0, 0);
                            };
                        }*/

    height2normal(bcanvas);

    //bctx.drawImage(normalmaptexture, 0, 0);

    var data = bctx.getImageData(0, 0, width, height).data;
    //console.log(data);
    
        
        // precalculate the normals
        for(var i = 0; i < bcanvas.height*bcanvas.width*4; i+=4) {
            var nx = data[i];
            // flip the y value
            var ny = 255-data[i+1];
            var nz = data[i+2];

            // normalize
            var magInv = 1.0/Math.sqrt(nx*nx + ny*ny + nz*nz);
            nx *= magInv;
            ny *= magInv;
            nz *= magInv;

            normals.push(nx);
            normals.push(ny);
            normals.push(nz);
        }
        //bctx.clearRect(0, 0, bcanvas.width, bcanvas.height);
        //var textureImg = loadImage(texture, function() {
            //textureData = getDataFromImage(textureImg).data;
            textureData = ctx.getImageData(0, 0, width, height).data;
            main();
        //});

    


    function main() {
        var rect = canvas.getBoundingClientRect();
        drawLight(canvas, ctx, normals, textureData, 0, spec.value/10, width*1.125, height*1.5, 500);
        /*canvas.onmousemove = function (e)
        {
            lightx.value = e.clientX;
            lighty.value = e.clientY;
            drawLight(canvas, ctx, normals, textureData, shine.value, spec.value, e.clientX + 10, e.clientY + 10, lightz.value);
        }
        lightx.onmouseout = function (e)
        {
            //lightx.value = e.clientX;
            //lighty.value = e.clientY;
            drawLight(canvas, ctx, normals, textureData, shine.value, spec.value, lightx.value, lighty.value, lightz.value);
        }
        lighty.onmouseout = function (e)
        {
            //lightx.value = e.clientX;
            //lighty.value = e.clientY;
            drawLight(canvas, ctx, normals, textureData, shine.value, spec.value, lightx.value, lighty.value, lightz.value);
        }*/

    }
//}





}


function height2normal( canvas ) {
    var context = canvas.getContext( '2d' );
    var width = canvas.width;
    var height = canvas.height;
    var src = context.getImageData( 0, 0, width, height );
    var dst = context.createImageData( width, height );
    var lightx = document.getElementById("lightx");
    var bias = document.getElementById("Bias");
    var biascalc = 1 - (((100 - bias.value) - .1) / 100);
    console.log("bias: " + bias.value + "biascalc: " + biascalc);

    for ( var i = 0, l = width * height * 4; i < l; i += 4 ) {
      var x1, x2, y1, y2;
      if ( i % ( width * 4 ) == 0 ) {
        // left edge
        x1 = src.data[ i ];
        x2 = src.data[ i + 4 ];
      } else if ( i % ( width * 4 ) == ( width - 1 ) * 4 ) {
        // right edge
        x1 = src.data[ i - 4 ];
        x2 = src.data[ i ];
      } else {
        x1 = src.data[ i - 4 ];
        x2 = src.data[ i + 4 ];
      }
      if ( i < width * 4 ) {
        // top edge
        y1 = src.data[ i ];
        y2 = src.data[ i + width * 4 ];
      } else if ( i > width * ( height - 1 ) * 4) {
        // bottom edge
        y1 = src.data[ i - width * 4 ];
        y2 = src.data[ i ];
      } else {
        y1 = src.data[ i - width * 4 ];
        y2 = src.data[ i + width * 4 ];
      }

      //var scalef = 1;
      var z1 = x1 - x2;
      var z2 = y2 - y1;

      
      

      
      //var dstMag = Math.sqrt((x1 - x2)*(x1 - x2) + (y2 - y1) * (y2 - y1) + 1); //(1.0 - ((100 - 0.1) / 100.0)) * (1.0 - ((100 - 0.1) / 100.0)));
      var dstMag = Math.sqrt((z1 * z1) + (z2 * z2) + (biascalc*biascalc));
      //console.log("dstMag " + dstMag);

      /*dst.data[ i ] = ( x1 - x2 )*.5/dstMag + 127; //(1.0 - ((100 - 0.1) / 100.0))*255; //127;
      dst.data[ i + 1 ] = ( y2 - y1 )*.5/dstMag + 127; //(1.0 - ((100 - 0.1) / 100.0))*255; //127;
      dst.data[ i + 2 ] = 255*.5/dstMag + 127;
      dst.data[ i + 3 ] = 255;*/

      dst.data[ i ] = (biascalc*128)*z1 + 128; //255 * (z1/dstMag + 1)/2;
      dst.data[ i + 1 ] =  (biascalc*128)*z2 + 128; //255 * (z2/dstMag + 1)/2;
      dst.data[ i + 2 ] = (128)*biascalc + 128; //255 * (biascal/dstMag + 1)/2;
      dst.data[ i + 3 ] = 255;
    }
    context.putImageData( dst, 0, 0 );
  }



function drawLight(canvas, ctx, normals, textureData, shiny, specularity, lx, ly, lz) {
    var imgData = ctx.getImageData(0, 0, canvas.width, canvas.height);
    var data = imgData.data;
    var i = 0;
    var ni = 0;
    var dx = 0, dy = 0, dz = 0;
    for(var y = 0; y < canvas.height; y++) {
        for(var x = 0; x < canvas.width; x++) {
            // get surface normal
            nx = normals[ni];
            ny = normals[ni+1];
            nz = normals[ni+2];

            // make it a bit faster by only updateing the direction
            // for every other pixel
            if(shiny > 0 || (ni&1) == 0){
                // calculate the light direction vector
                dx = lx - x;
                dy = ly - y;
                dz = lz;

                // normalize it
                magInv = 1.0/Math.sqrt(dx*dx + dy*dy + dz*dz);
                dx *= magInv;
                dy *= magInv;
                dz *= magInv;
            }

            // take the dot product of the direction and the normal
            // to get the amount of specularity
            var dot = dx*nx + dy*ny + dz*nz;
            var spec = Math.pow(dot, 20)*specularity;
            spec += Math.pow(dot, 400)*shiny;
            // spec + ambient
            var intensity = spec + .7;

            for(var channel = 0; channel < 3; channel++) {
                data[i+channel] = Math.round(clamp(textureData[i+channel]*intensity, 0, 255));
            }
            i += 4;
            ni += 3;
        }
    }
    ctx.putImageData(imgData, 0, 0);
}



function clamp(x, min, max) {
    if(x < min) return min;
    if(x > max) return max-1;
    return x;
}




function createBumpMap()
        {


            var bcanvas = document.getElementById("bCanvas");
            var bctx = bcanvas.getContext('2d');

            var xt = 0;
            var yt = 0;

            var scaleCorrection = scale;//1 / scale;
            

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {

                        xt = (x + translated) % width;
                        if (translated < 0)
                        {
                            
                            xt = (width + (x - (Math.abs(translated) % width))) % width;
                            
                        }

                        var scaleDiffY = -1 * (height * (scaleCorrection - 1) / scaleCorrection);
                        if (translatedY > 0)
                        {
                            translatedY = 0;
                        }
                        if (translatedY < scaleDiffY)
                        {
                            translatedY = Math.floor(scaleDiffY);
                            //yt = y + translatedY;
                        }
                        yt = y + translatedY;
                       
                        if (scaleCorrection <= 1)
                        {
                            yt = y;
                        }
    



                        bctx.fillStyle = colorLuminance("#FFFFFF", ModifiedElevationArray[x][y]);

                        if (ModifiedElevationArray[x][y] <= sea_level)
                        {
                            bctx.fillStyle = "#808080";


                        }
                        if (RiverLayer[x][y] > 0)
                        {
                            bctx.fillStyle = "#808080";

                        }
                    
                    /*if (BiomeArray[x][y] == "ICE")
                    {
                    ctx.fillStyle = "#b3b3b3";
                    ctx2.fillStyle = "#b3b3b3";
                    }*/
                    bctx.fillRect(xt, yt, 1, 1);




                }
            }

            var preContrastImg = bctx.getImageData(0, 0, width, height);
            var postContrastImg = contrastImage(preContrastImg, 50);
            bctx.putImageData(postContrastImg, 0, 0);

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {

                    
                        xt = (x + translated) % width;
                        if (translated < 0)
                        {
                            
                            xt = (width + (x - (Math.abs(translated) % width))) % width;
                            
                        }

                        var scaleDiffY = -1 * (height * (scaleCorrection - 1) / scaleCorrection);
                        if (translatedY > 0)
                        {
                            translatedY = 0;
                        }
                        if (translatedY < scaleDiffY)
                        {
                            translatedY = Math.floor(scaleDiffY);
                            //yt = y + translatedY;
                        }
                        yt = y + translatedY;
                       
                        if (scaleCorrection <= 1)
                        {
                            yt = y;
                        }


                    //ctx.fillStyle = colorLuminance("#FFFFFF", ElevationArray[x][y]);
                    //ctx2.fillStyle = colorLuminance("#FFFFFF", ElevationArray[x][y]);
                    if (ModifiedElevationArray[x][y] <= sea_level + .005)
                    {
                        //bctx.fillStyle = "#000";
                        bctx.fillStyle = "#808080";
                        //ctx2.fillStyle = "#808080";
                        bctx.fillRect(xt, yt, 1, 1);

                    }
                    if (BiomeMap[x][y] == "ICE")
                    {
                        bctx.fillStyle = "#b3b3b3";
                        bctx.fillRect(xt, yt, 1, 1);
                    }





                }
            }

        }


function contrastImage(imageData, contrast)
        {

            var data = imageData.data;
            var factor = (259 * (contrast + 255)) / (255 * (259 - contrast));

            for (var i = 0; i < data.length; i += 4)
            {
                data[i] = factor * (data[i] - 128) + 128;
                data[i + 1] = factor * (data[i + 1] - 128) + 128;
                data[i + 2] = factor * (data[i + 2] - 128) + 128;
            }
            return imageData;
        }


function drawLoadBar(progress_percent)
{

    //alert("in load function");

    var leg = document.getElementById("legendCanvas");
    var legctx = leg.getContext("2d");

    legctx.clearRect(width / 4, 31, width / 2, 38);
    legctx.fillStyle = "yellow";
    legctx.fillRect(width/4, 31, width/2*progress_percent, 38);
    legctx.font = "20px Arial";
    legctx.strokeStyle = "black";
    legctx.lineWidth = 5;
    legctx.strokeText(Math.floor(progress_percent * 100)+"%", width / 2, 58);
    legctx.fillStyle = "white";
    legctx.fillText(Math.floor(progress_percent*100)+"%", width / 2, 58);

    //legctx.fillRect(200,30,50,40);


}


/*Object.prototype.clone = function() {
    var newObj = (this instanceof Array) ? [] : {};
    for (i in this) {
        if (i == 'clone') 
            continue;
        if (this[i] && typeof this[i] == "object") {
            newObj[i] = this[i].clone();
        } 
        else 
            newObj[i] = this[i]
    } return newObj;
};*/


function deepObjCopy (dupeObj) {
	var retObj = new Object();
	if (typeof(dupeObj) == 'object') {
		if (typeof(dupeObj.length) != 'undefined')
			var retObj = new Array();
		for (var objInd in dupeObj) {	
			if (typeof(dupeObj[objInd]) == 'object') {
				retObj[objInd] = deepObjCopy(dupeObj[objInd]);
			} else if (typeof(dupeObj[objInd]) == 'string') {
				retObj[objInd] = dupeObj[objInd];
			} else if (typeof(dupeObj[objInd]) == 'number') {
				retObj[objInd] = dupeObj[objInd];
			} else if (typeof(dupeObj[objInd]) == 'boolean') {
				((dupeObj[objInd] == true) ? retObj[objInd] = true : retObj[objInd] = false);
			}
		}
	}
	return retObj;
}


function textWidth(text, fontProp) {
    var tag = document.createElement("div");
    tag.style.position = "absolute";
    tag.style.left = "-999em";
    tag.style.whiteSpace = "nowrap";
    tag.style.font = fontProp;
    tag.innerHTML = text;

    document.body.appendChild(tag);

    var result = tag.clientWidth;

    document.body.removeChild(tag);

    return result;
}

function textHeight(text, fontProp) {
    var tag = document.createElement("div");
    tag.style.position = "absolute";
    tag.style.left = "-999em";
    tag.style.whiteSpace = "nowrap";
    tag.style.font = fontProp;
    tag.innerHTML = text;

    document.body.appendChild(tag);

    var result = tag.clientHeight;

    document.body.removeChild(tag);

    return result;
}


function showPlanet()
{
   window.open("3D Sphere Map/index.html");  

}

function showPlane()
{
    window.open("3D Plane Map/index.html");
}