variable "prefix" {
    type = string
    default = "acronisupdater"
}

variable "location" {
    type = string
    default = "westeurope"
}

variable "functionapp" {
    type = string
    default = "../UpdateFunction/bin/publish/UpdateFunction.zip"
}