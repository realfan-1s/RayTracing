using System.Collections.Generic;

public class xComparer : IComparer<Sphere>{
    public int Compare(Sphere x, Sphere y){
        if (x.pos.x < y.pos.x)
            return -1;
        else if (x.pos.x > y.pos.x)
            return 1;
        return 0;
    }
}

public class yComparer : IComparer<Sphere>{
    public int Compare(Sphere x, Sphere y){
        if (x.pos.y < y.pos.y)
            return -1;
        else if (x.pos.y > y.pos.y)
            return 1;
        return 0;
    }
}

public class zComparer : IComparer<Sphere>{
    public int Compare(Sphere x, Sphere y){
        if (x.pos.z < y.pos.z)
            return -1;
        else if (x.pos.z > y.pos.z)
            return 1;
        return 0;
    }
}

public enum SortType{
    xSort,
    ySort,
    zSort
}